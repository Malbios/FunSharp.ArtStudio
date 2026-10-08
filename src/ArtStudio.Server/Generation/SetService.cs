using ArtStudio.Server.Data;
using ArtStudio.Server.DeviantArt;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Hubs;
using ArtStudio.Server.Vision;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.Generation;

public abstract record SetSource
{
    public sealed record None : SetSource;

    public sealed record ImageFile(SourceKind Kind, Stream Content, string Extension) : SetSource;


    public sealed record DeviantArtUrl(string Url, int ImageIndex) : SetSource;
}

public sealed record NewSetRequest(string Prompt, string Resolution, int Count, SetSource Source, bool AsDraft = false);

public sealed class SetService(
    StudioDbContext db,
    AppPaths paths,
    QueueService queueService,
    DeviantArtService deviantArtService,
    GenerationQueue queue,
    PromptGenerationQueue promptQueue,
    VisionApiKey visionApiKey,
    StudioNotifier notifier,
    TimeProvider clock)
{
    private static readonly TimeSpan RunningJobStopTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RunningJobPollInterval = TimeSpan.FromMilliseconds(200);

    public async Task<PromptSet> CreateAsync(NewSetRequest request, CancellationToken ct)
    {
        if (request.AsDraft && request.Source is SetSource.None)
            throw new UserFacingException("Attach an image to save a draft.");
        if (!request.AsDraft && string.IsNullOrWhiteSpace(request.Prompt))
            throw new UserFacingException("The prompt must not be empty.");
        var resolution = Resolutions.Find(request.Resolution)
            ?? throw new UserFacingException($"Unknown resolution '{request.Resolution}'.");

        var set = new PromptSet
        {
            Prompt = request.Prompt.Trim(),
            Resolution = resolution.Name,
            SourceKind = SourceKind.None,
            CreatedAt = clock.GetUtcNow(),
            IsDraft = request.AsDraft,
        };


        var deviation = request.Source is SetSource.DeviantArtUrl deviantArt
            ? await deviantArtService.DownloadAsync(deviantArt.Url, deviantArt.ImageIndex, ct)
            : null;

        db.PromptSets.Add(set);
        await db.SaveChangesAsync(ct);

        switch (request.Source)
        {
            case SetSource.ImageFile file:
                await AttachSourceImageAsync(set, file.Kind, file.Content, file.Extension, ct);
                break;
            case SetSource.DeviantArtUrl when deviation is not null:
                await AttachDeviationAsync(set, deviation, ct);
                break;
        }

        if (set.IsDraft)
            await QueuePromptGenerationIfKeySetAsync(set, ct);
        else
            await queueService.EnqueueAsync(set.Id, request.Count, prompt: null, resolution: null, ct);
        return set;
    }

    public async Task<PromptSet> CreateDeviantArtDraftAsync(string url, CancellationToken ct)
    {
        var deviation = await deviantArtService.DownloadAsync(url, imageIndex: 0, ct);
        var mainImage = deviation.Preview.Images[0];

        var set = new PromptSet
        {
            Prompt = "",
            Resolution = Resolutions.ClosestTo(mainImage.Width, mainImage.Height).Name,
            SourceKind = SourceKind.None,
            CreatedAt = clock.GetUtcNow(),
            IsDraft = true,
        };
        db.PromptSets.Add(set);
        await db.SaveChangesAsync(ct);

        await AttachDeviationAsync(set, deviation, ct);
        await QueuePromptGenerationIfKeySetAsync(set, ct);
        return set;
    }

    private async Task AttachDeviationAsync(PromptSet set, DownloadedDeviation deviation, CancellationToken ct)
    {
        set.DeviantArtUrl = deviation.Preview.Url;
        set.DeviationId = deviation.Preview.DeviationId;
        set.DeviantArtAuthor = deviation.Preview.Author;
        await AttachSourceImageAsync(set, SourceKind.DeviantArt, new MemoryStream(deviation.Content), deviation.Extension, ct);
    }

    public async Task QueueDraftAsync(int setId, string prompt, string resolution, int count, CancellationToken ct)
    {
        var set = await FindSetAsync(setId, ct);
        if (!set.IsDraft)
            throw new UserFacingException("This set is not a draft.");

        set.IsDraft = false;
        if (set.PromptGeneration is PromptGenerationState.Queued or PromptGenerationState.Running)
            set.PromptGeneration = PromptGenerationState.None;
        await queueService.EnqueueAsync(setId, count, prompt, resolution, ct);
        promptQueue.CancelRunning(setId);
        await notifier.SetUpdated(setId);
    }

    public async Task QueuePromptGenerationAsync(int setId, CancellationToken ct)
    {
        var set = await FindSetAsync(setId, ct);
        if (set.SourceImagePath is null)
            throw new UserFacingException("This set has no inspiration image to describe.");
        await EnsureVisionCanStartAsync(set, ct);

        SetModification(set, instructions: null, basePrompt: null, paragraphIndex: null, section: null);
        await MarkPromptGenerationQueuedAsync(set, ct);
    }

    public async Task QueuePromptModificationAsync(
        int setId, string prompt, string instructions, int? paragraphIndex, string? section, CancellationToken ct)
    {
        var set = await FindSetAsync(setId, ct);
        if (string.IsNullOrWhiteSpace(prompt))
            throw new UserFacingException("There is no prompt text to modify.");
        if (string.IsNullOrWhiteSpace(instructions))
            throw new UserFacingException("Describe how the prompt should change.");
        if (paragraphIndex is { } index && (index < 0 || index >= PromptModifier.Paragraphs(prompt).Count))
            throw new UserFacingException("That paragraph is not part of the prompt.");
        await EnsureVisionCanStartAsync(set, ct);

        SetModification(set, instructions.Trim(), prompt.Trim(), paragraphIndex, section?.Trim());
        await MarkPromptGenerationQueuedAsync(set, ct);
    }

    private async Task EnsureVisionCanStartAsync(PromptSet set, CancellationToken ct)
    {
        if (set.PromptGeneration is PromptGenerationState.Queued or PromptGenerationState.Running)
            throw new UserFacingException("A prompt for this set is already being generated.");
        if (!await visionApiKey.HasKeyAsync(ct))
            throw new UserFacingException("Set the vision API key in Settings first.");
    }

    private static void SetModification(PromptSet set, string? instructions, string? basePrompt, int? paragraphIndex, string? section)
    {
        set.ModifyInstructions = instructions;
        set.ModifyBasePrompt = basePrompt;
        set.ModifyParagraphIndex = paragraphIndex;
        set.ModifySection = section;
    }

    private async Task QueuePromptGenerationIfKeySetAsync(PromptSet set, CancellationToken ct)
    {
        if (await visionApiKey.HasKeyAsync(ct))
            await MarkPromptGenerationQueuedAsync(set, ct);
        else
            await notifier.SetUpdated(set.Id);
    }

    private async Task MarkPromptGenerationQueuedAsync(PromptSet set, CancellationToken ct)
    {
        set.PromptGeneration = PromptGenerationState.Queued;
        set.PromptGenerationQueuedAt = clock.GetUtcNow();
        set.PromptGenerationError = null;
        set.PromptGenerationTruncated = false;
        await db.SaveChangesAsync(ct);
        promptQueue.Wake();
        await notifier.SetUpdated(set.Id);
    }

    public async Task PickAsync(int setId, int imageId, CancellationToken ct)
    {
        if (!await db.Images.AnyAsync(i => i.Id == imageId && i.PromptSetId == setId, ct))
            throw new UserFacingException("That image does not belong to this set.");
        if (await db.Picks.AnyAsync(p => p.PromptSetId == setId && p.GeneratedImageId == imageId, ct))
            return;

        var lastPosition = await db.Picks.Where(p => p.PromptSetId == setId).MaxAsync(p => (int?)p.Position, ct) ?? 0;
        db.Picks.Add(new PickedImage { PromptSetId = setId, GeneratedImageId = imageId, Position = lastPosition + 1 });
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkReadyToPostAsync(int setId, CancellationToken ct)
    {
        var set = await FindSetAsync(setId, ct);
        EnsureNotArchived(set);
        if (!await db.Picks.AnyAsync(p => p.PromptSetId == setId, ct))
            throw new UserFacingException("Pick at least one image before moving the set to Post.");
        if (await db.Jobs.AnyAsync(j => j.PromptSetId == setId && (j.Status == JobStatus.Queued || j.Status == JobStatus.Running), ct))
            throw new UserFacingException("Wait for or cancel the set's queued and running jobs first.");

        set.ReadyToPostAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await notifier.SetUpdated(setId);
    }

    public async Task MoveBackToSetsAsync(int setId, CancellationToken ct)
    {
        var set = await FindSetAsync(setId, ct);
        EnsureNotArchived(set);
        set.ReadyToPostAt = null;
        await db.SaveChangesAsync(ct);
        await notifier.SetUpdated(setId);
    }

    internal static void EnsureNotArchived(PromptSet set)
    {
        if (set.ArchivedAt is not null)
            throw new UserFacingException("Restore the set from the archive first.");
    }

    private async Task<PromptSet> FindSetAsync(int setId, CancellationToken ct) =>
        await db.PromptSets.SingleOrDefaultAsync(s => s.Id == setId, ct) ?? throw new UserFacingException("Set not found.");

    public async Task UnpickAsync(int setId, int imageId, CancellationToken ct)
    {
        var picks = await LoadPicksAsync(setId, ct);
        var removed = picks.FirstOrDefault(p => p.GeneratedImageId == imageId);
        if (removed is null)
            return;
        if (picks.Count == 1 && await db.PromptSets.AnyAsync(s => s.Id == setId && s.ReadyToPostAt != null, ct))
            throw new UserFacingException("A set that is ready to post needs at least one pick. Move it back to Sets first.");

        db.Picks.Remove(removed);
        Renumber(picks.Where(p => p != removed));
        await db.SaveChangesAsync(ct);
    }

    public async Task ReorderPicksAsync(int setId, IReadOnlyList<int> imageIds, CancellationToken ct)
    {
        var picks = await LoadPicksAsync(setId, ct);
        if (imageIds.Count != picks.Count || !imageIds.Order().SequenceEqual(picks.Select(p => p.GeneratedImageId).Order()))
            throw new UserFacingException("The new order must contain exactly the picked images. Reload and try again.");

        var picksByImageId = picks.ToDictionary(p => p.GeneratedImageId);
        Renumber(imageIds.Select(id => picksByImageId[id]));
        await db.SaveChangesAsync(ct);
    }

    private Task<List<PickedImage>> LoadPicksAsync(int setId, CancellationToken ct) =>
        db.Picks.Where(p => p.PromptSetId == setId).OrderBy(p => p.Position).ToListAsync(ct);

    private static void Renumber(IEnumerable<PickedImage> picksInOrder)
    {
        var position = 1;
        foreach (var pick in picksInOrder)
            pick.Position = position++;
    }

    public async Task<bool> DeleteAsync(int setId, CancellationToken ct)
    {
        if (!await db.PromptSets.AnyAsync(s => s.Id == setId, ct))
            return false;

        await StopJobsAsync(setId, [JobStatus.Queued], ct);

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            await db.Picks.Where(p => p.PromptSetId == setId).ExecuteDeleteAsync(ct);
            await db.Images.Where(i => i.PromptSetId == setId).ExecuteDeleteAsync(ct);
            await db.Jobs.Where(j => j.PromptSetId == setId).ExecuteDeleteAsync(ct);
            await db.PromptSets.Where(s => s.Id == setId).ExecuteDeleteAsync(ct);
            await transaction.CommitAsync(ct);
        }

        promptQueue.CancelRunning(setId);
        await notifier.SetDeleted(setId);
        return true;
    }

    public async Task ArchiveAsync(int setId, CancellationToken ct)
    {
        var set = await FindSetAsync(setId, ct);
        if (set.IsDraft)
            throw new UserFacingException("Drafts cannot be archived. Delete the draft instead.");
        if (set.ArchivedAt is not null)
            throw new UserFacingException("This set is already archived.");

        await StopJobsAsync(setId, [JobStatus.Queued, JobStatus.Failed], ct);

        set.ArchivedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await notifier.SetUpdated(setId);
    }

    public async Task RestoreAsync(int setId, CancellationToken ct)
    {
        var set = await FindSetAsync(setId, ct);
        set.ArchivedAt = null;
        await db.SaveChangesAsync(ct);
        await notifier.SetUpdated(setId);
    }

    private async Task StopJobsAsync(int setId, JobStatus[] statusesToCancel, CancellationToken ct)
    {
        var cancelledJobIds = await db.Jobs
            .Where(j => j.PromptSetId == setId && statusesToCancel.Contains(j.Status))
            .Select(j => j.Id)
            .ToListAsync(ct);
        await db.Jobs
            .Where(j => cancelledJobIds.Contains(j.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, JobStatus.Cancelled)
                .SetProperty(j => j.FinishedAt, clock.GetUtcNow()), ct);
        foreach (var jobId in cancelledJobIds)
            await notifier.JobUpdated(jobId, setId);

        await StopRunningJobAsync(setId, ct);
    }

    private async Task StopRunningJobAsync(int setId, CancellationToken ct)
    {
        var runningJobId = await RunningJobIdAsync(setId, ct);
        if (runningJobId is null)
            return;

        queue.CancelRunning(runningJobId.Value);
        var deadline = clock.GetUtcNow() + RunningJobStopTimeout;
        while (await RunningJobIdAsync(setId, ct) is not null)
        {
            if (clock.GetUtcNow() > deadline)
                throw new UserFacingException("ComfyUI did not stop the running job in time. Please try again.");
            await Task.Delay(RunningJobPollInterval, ct);
        }
    }

    private Task<int?> RunningJobIdAsync(int setId, CancellationToken ct) =>
        db.Jobs
            .Where(j => j.PromptSetId == setId && j.Status == JobStatus.Running)
            .Select(j => (int?)j.Id)
            .FirstOrDefaultAsync(ct);

    private async Task AttachSourceImageAsync(
        PromptSet set, SourceKind kind, Stream content, string extension, CancellationToken ct)
    {
        var settings = await SettingsStore.LoadAsync(db, paths, ct);
        set.SourceKind = kind;
        set.SourceImagePath = await ImageStore.SaveSourceAsync(settings.OutputDirectory, set.Id, content, extension, ct);
        await db.SaveChangesAsync(ct);
    }

}
