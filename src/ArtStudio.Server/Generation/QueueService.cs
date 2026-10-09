using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Hubs;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.Generation;

public class UserFacingException(string message) : Exception(message);

public sealed class QueueService(
    StudioDbContext db, AppPaths paths, GenerationQueue queue, StudioNotifier notifier, TimeProvider clock)
{
    public const int MaxImagesPerJob = 100;

    /// <summary>Queues a batch for the set; a missing prompt or resolution falls back to the set's most recent one.</summary>
    public async Task<GenerationJob> EnqueueAsync(
        int setId, int count, string? prompt, string? resolution, CancellationToken ct)
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

        var lastPosition = await db.Jobs.MaxAsync(j => (long?)j.QueuePosition, ct) ?? 0;
        var job = new GenerationJob
        {
            PromptSetId = setId,
            Prompt = set.Prompt,
            Resolution = set.Resolution,
            RequestedCount = count,
            Status = JobStatus.Queued,
            QueuePosition = lastPosition + 1,
            CreatedAt = clock.GetUtcNow(),
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);

        await notifier.JobUpdated(job.Id, setId);
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
        await SetPausedAsync(false, ct);
    }

    /// <summary>Swaps a waiting job with its waiting neighbour; offset -1 runs it earlier, +1 later.</summary>
    public async Task MoveAsync(int jobId, int offset, CancellationToken ct)
    {
        if (offset is not (-1 or 1))
            throw new UserFacingException("A job moves one place at a time.");

        var waiting = await db.Jobs
            .Where(j => j.Status == JobStatus.Queued)
            .OrderBy(j => j.QueuePosition)
            .ToListAsync(ct);
        var index = waiting.FindIndex(j => j.Id == jobId);
        if (index < 0)
            throw new UserFacingException("Only waiting jobs can be moved.");
        var neighbourIndex = index + offset;
        if (neighbourIndex < 0)
            throw new UserFacingException("This job is already first.");
        if (neighbourIndex >= waiting.Count)
            throw new UserFacingException("This job is already last.");

        var (job, neighbour) = (waiting[index], waiting[neighbourIndex]);
        (job.QueuePosition, neighbour.QueuePosition) = (neighbour.QueuePosition, job.QueuePosition);
        await db.SaveChangesAsync(ct);

        await notifier.JobUpdated(job.Id, job.PromptSetId);
        await notifier.JobUpdated(neighbour.Id, neighbour.PromptSetId);
    }

    public async Task SetPausedAsync(bool paused, CancellationToken ct)
    {
        var settings = await SettingsStore.LoadAsync(db, paths, ct);
        settings.QueuePaused = paused;
        await db.SaveChangesAsync(ct);

        await notifier.QueueStateChanged(paused);
        if (!paused)
            queue.Wake();
    }
}
