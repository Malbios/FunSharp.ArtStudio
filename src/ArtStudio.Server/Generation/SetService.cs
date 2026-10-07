using ArtStudio.Server.Data;
using ArtStudio.Server.DeviantArt;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Hubs;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.Generation;

public abstract record SetSource
{
    public sealed record None : SetSource;

    public sealed record ImageFile(SourceKind Kind, Stream Content, string Extension) : SetSource;


    public sealed record DeviantArtUrl(string Url) : SetSource;
}

public sealed record NewSetRequest(string Prompt, string Resolution, int Count, SetSource Source);

public sealed class SetService(
    StudioDbContext db,
    AppPaths paths,
    QueueService queueService,
    DeviantArtService deviantArtService,
    GenerationQueue queue,
    StudioNotifier notifier,
    TimeProvider clock)
{
    private static readonly TimeSpan RunningJobStopTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RunningJobPollInterval = TimeSpan.FromMilliseconds(200);

    public async Task<PromptSet> CreateAsync(NewSetRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new UserFacingException("The prompt must not be empty.");
        var resolution = Resolutions.Find(request.Resolution)
            ?? throw new UserFacingException($"Unknown resolution '{request.Resolution}'.");

        var set = new PromptSet
        {
            Prompt = request.Prompt.Trim(),
            Resolution = resolution.Name,
            SourceKind = SourceKind.None,
            CreatedAt = clock.GetUtcNow(),
        };


        var deviation = request.Source is SetSource.DeviantArtUrl deviantArt
            ? await deviantArtService.DownloadAsync(deviantArt.Url, ct)
            : null;

        db.PromptSets.Add(set);
        await db.SaveChangesAsync(ct);

        switch (request.Source)
        {
            case SetSource.ImageFile file:
                await AttachSourceImageAsync(set, file.Kind, file.Content, file.Extension, ct);
                break;
            case SetSource.DeviantArtUrl when deviation is not null:
                set.DeviantArtUrl = deviation.Preview.Url;
                set.DeviationId = deviation.Preview.DeviationId;
                set.DeviantArtAuthor = deviation.Preview.Author;
                await AttachSourceImageAsync(set, SourceKind.DeviantArt, new MemoryStream(deviation.Content), deviation.Extension, ct);
                break;
        }

        await queueService.EnqueueAsync(set.Id, request.Count, prompt: null, resolution: null, ct);
        return set;
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

    public async Task UnpickAsync(int setId, int imageId, CancellationToken ct)
    {
        var picks = await LoadPicksAsync(setId, ct);
        var removed = picks.FirstOrDefault(p => p.GeneratedImageId == imageId);
        if (removed is null)
            return;

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

        await db.Jobs
            .Where(j => j.PromptSetId == setId && j.Status == JobStatus.Queued)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, JobStatus.Cancelled)
                .SetProperty(j => j.FinishedAt, clock.GetUtcNow()), ct);
        await StopRunningJobAsync(setId, ct);

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            await db.Picks.Where(p => p.PromptSetId == setId).ExecuteDeleteAsync(ct);
            await db.Images.Where(i => i.PromptSetId == setId).ExecuteDeleteAsync(ct);
            await db.Jobs.Where(j => j.PromptSetId == setId).ExecuteDeleteAsync(ct);
            await db.PromptSets.Where(s => s.Id == setId).ExecuteDeleteAsync(ct);
            await transaction.CommitAsync(ct);
        }

        await notifier.SetDeleted(setId);
        return true;
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
