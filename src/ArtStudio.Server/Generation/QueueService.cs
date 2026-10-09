using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
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

    /// <summary>Moves a waiting job among the waiting jobs; the others keep their relative order.</summary>
    public async Task MoveAsync(int jobId, QueueMove move, CancellationToken ct)
    {
        var waiting = await db.Jobs
            .Where(j => j.Status == JobStatus.Queued)
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
