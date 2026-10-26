using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Google;
using ArtStudio.Server.Hubs;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.Generation;

public class UserFacingException(string message) : Exception(message);

public enum QueueMove
{
    Top,
    Up,
    Down,
    Bottom,
}

public sealed record VideoJobRequest(
    string StartImagePath, string? EndImagePath, string Prompt, double Seconds, double Megapixels, bool Turbo, int Count,
    bool QueueFirst);

public sealed class QueueService(
    StudioDbContext db, AppPaths paths, GenerationQueue queue, StudioNotifier notifier, GoogleAuth googleAuth, TimeProvider clock)
{
    public const int MaxImagesPerJob = 100;

    /// <summary>
    /// Queues a batch for the set, after the waiting jobs or, with <paramref name="queueFirst"/>, ahead of them;
    /// a missing prompt or resolution falls back to the set's most recent one.
    /// </summary>
    public async Task<GenerationJob> EnqueueAsync(
        int setId, int count, string? prompt, string? resolution, bool queueFirst, CancellationToken ct)
    {
        if (count is < 1 or > MaxImagesPerJob)
            throw new UserFacingException($"Image count must be between 1 and {MaxImagesPerJob}.");

        var set = await db.PromptSets.SingleOrDefaultAsync(s => s.Id == setId, ct)
            ?? throw new UserFacingException("Set not found.");
        if (set.IsDraft)
            throw new UserFacingException("Add a prompt and queue the draft first.");
        SetService.EnsureNotArchived(set);
        if (set.ReadyToPostAt is not null)
            throw new UserFacingException("This set is ready to post. Move it back to Sets to generate more images.");
        var jobPrompt = prompt ?? set.Prompt;
        if (string.IsNullOrWhiteSpace(jobPrompt))
            throw new UserFacingException("The prompt must not be empty.");
        var preset = Resolutions.Find(resolution ?? set.Resolution)
            ?? throw new UserFacingException($"Unknown resolution '{resolution}'.");

        set.Prompt = jobPrompt.Trim();
        set.Resolution = preset.Name;

        return await AddJobAsync(new GenerationJob
        {
            PromptSetId = setId,
            Kind = JobKind.Image,
            Prompt = set.Prompt,
            Resolution = set.Resolution,
            RequestedCount = count,
        }, queueFirst, ct);
    }

    /// <summary>Queues videos of the set: the start image (and optional end image) set the first (and last) frame.</summary>
    public async Task<GenerationJob> EnqueueVideoAsync(int setId, VideoJobRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new UserFacingException("The prompt must not be empty.");
        await EnsureCanQueueVideoAsync(request.Seconds, request.Megapixels, request.Count, ct);
        var set = await db.PromptSets.SingleOrDefaultAsync(s => s.Id == setId, ct)
            ?? throw new UserFacingException("Set not found.");
        if (set.IsDraft)
            throw new UserFacingException("Add a prompt and queue the draft first.");
        SetService.EnsureNotArchived(set);

        return await AddJobAsync(new GenerationJob
        {
            PromptSetId = setId,
            Kind = JobKind.Video,
            Prompt = request.Prompt.Trim(),
            Resolution = VideoOptions.ResolutionLabel(request.Megapixels),
            RequestedCount = request.Count,
            VideoSeconds = request.Seconds,
            VideoMegapixels = request.Megapixels,
            VideoTurbo = request.Turbo,
            VideoStartImagePath = request.StartImagePath,
            VideoEndImagePath = request.EndImagePath,
        }, request.QueueFirst, ct);
    }

    public async Task EnsureCanQueueVideoAsync(double seconds, double megapixels, int count, CancellationToken ct)
    {
        VideoOptions.Validate(seconds, megapixels, count);
        if (!await googleAuth.IsConnectedAsync(ct))
            throw new UserFacingException("Videos are fetched from Google Drive. Connect Google Drive in Settings first.");
    }

    private async Task<GenerationJob> AddJobAsync(GenerationJob job, bool queueFirst, CancellationToken ct)
    {
        // "First" means first among the waiting jobs of its own kind: it never makes ComfyUI switch models early.
        var firstQueuedPosition = queueFirst
            ? await db.Jobs.Where(j => j.Status == JobStatus.Queued && j.Kind == job.Kind).MinAsync(j => (long?)j.QueuePosition, ct)
            : null;
        var lastPosition = await db.Jobs.MaxAsync(j => (long?)j.QueuePosition, ct) ?? 0;
        job.Status = JobStatus.Queued;
        job.QueuePosition = firstQueuedPosition - 1 ?? lastPosition + 1;
        job.CreatedAt = clock.GetUtcNow();
        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);

        await notifier.JobUpdated(job.Id, job.PromptSetId);
        queue.Wake();
        return job;
    }

    public async Task CancelAsync(int jobId, CancellationToken ct)
    {
        var cancelledWhileQueued = await db.Jobs
            .Where(j => j.Id == jobId && j.Status == JobStatus.Queued)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, JobStatus.Cancelled)
                .SetProperty(j => j.FinishedAt, clock.GetUtcNow()), ct);

        if (cancelledWhileQueued == 0 && !queue.CancelRunning(jobId))
            throw new UserFacingException("Only queued or running jobs can be cancelled.");

        var setId = await db.Jobs.Where(j => j.Id == jobId).Select(j => j.PromptSetId).SingleAsync(ct);
        await notifier.JobUpdated(jobId, setId);
    }

    public async Task RetryAsync(int jobId, CancellationToken ct)
    {
        var job = await db.Jobs.SingleOrDefaultAsync(j => j.Id == jobId, ct)
            ?? throw new UserFacingException("Job not found.");
        if (job.Status != JobStatus.Failed)
            throw new UserFacingException("Only failed jobs can be retried.");

        var firstQueuedPosition = await db.Jobs
            .Where(j => j.Status == JobStatus.Queued)
            .MinAsync(j => (long?)j.QueuePosition, ct);
        job.QueuePosition = (firstQueuedPosition ?? job.QueuePosition) - 1;
        job.Status = JobStatus.Queued;
        job.Error = null;
        job.FinishedAt = null;
        await db.SaveChangesAsync(ct);

        await notifier.JobUpdated(job.Id, job.PromptSetId);
        await SetPausedAsync(job.Kind, false, ct);
    }

    /// <summary>Moves a waiting job among the waiting jobs of its kind; the others keep their relative order.</summary>
    public async Task MoveAsync(int jobId, QueueMove move, CancellationToken ct)
    {
        var kind = await db.Jobs.Where(j => j.Id == jobId).Select(j => (JobKind?)j.Kind).SingleOrDefaultAsync(ct);
        var waiting = await db.Jobs
            .Where(j => j.Status == JobStatus.Queued && j.Kind == kind)
            .OrderBy(j => j.QueuePosition)
            .ToListAsync(ct);
        var index = waiting.FindIndex(j => j.Id == jobId);
        if (index < 0)
            throw new UserFacingException("Only waiting jobs can be moved.");
        if (index == 0 && move is QueueMove.Top or QueueMove.Up)
            throw new UserFacingException("This job is already first.");
        if (index == waiting.Count - 1 && move is QueueMove.Down or QueueMove.Bottom)
            throw new UserFacingException("This job is already last.");

        var target = move switch
        {
            QueueMove.Top => 0,
            QueueMove.Up => index - 1,
            QueueMove.Down => index + 1,
            _ => waiting.Count - 1,
        };
        var positions = waiting.Select(j => j.QueuePosition).ToList();
        var job = waiting[index];
        waiting.RemoveAt(index);
        waiting.Insert(target, job);
        var moved = waiting.Where((candidate, position) => candidate.QueuePosition != positions[position]).ToList();
        for (var position = 0; position < waiting.Count; position++)
            waiting[position].QueuePosition = positions[position];
        await db.SaveChangesAsync(ct);

        foreach (var changed in moved)
            await notifier.JobUpdated(changed.Id, changed.PromptSetId);
    }

    /// <summary>Pauses or resumes the image or the video jobs, or both when no kind is given.</summary>
    public async Task SetPausedAsync(JobKind? kind, bool paused, CancellationToken ct)
    {
        var settings = await SettingsStore.LoadAsync(db, paths, ct);
        settings.SetPaused(kind, paused);
        await db.SaveChangesAsync(ct);

        await notifier.QueueStateChanged(settings.ImageQueuePaused, settings.VideoQueuePaused);
        if (!paused)
            queue.Wake();
    }
}
