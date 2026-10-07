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

    public sealed record CopyOf(int SetId) : SetSource;

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

        if (request.Source is SetSource.CopyOf copyOf)
            await CopySourceAsync(set, copyOf.SetId, ct);

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

        await queueService.EnqueueAsync(set.Id, request.Count, ct);
        return set;
    }

    public async Task SelectImageAsync(int setId, int? imageId, CancellationToken ct)
    {
        var set = await db.PromptSets.SingleOrDefaultAsync(s => s.Id == setId, ct)
            ?? throw new UserFacingException("Set not found.");
        if (imageId is not null && !await db.Images.AnyAsync(i => i.Id == imageId && i.PromptSetId == setId, ct))
            throw new UserFacingException("That image does not belong to this set.");

        set.SelectedImageId = imageId;
        await db.SaveChangesAsync(ct);
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

    private async Task CopySourceAsync(PromptSet target, int sourceSetId, CancellationToken ct)
    {
        var source = await db.PromptSets.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sourceSetId, ct)
            ?? throw new UserFacingException("The set to base this one on was not found.");

        target.SourceKind = source.SourceKind;
        target.SourceImagePath = source.SourceImagePath;
        target.DeviantArtUrl = source.DeviantArtUrl;
        target.DeviationId = source.DeviationId;
        target.DeviantArtAuthor = source.DeviantArtAuthor;
    }
}
