using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Generation;
using ArtStudio.Server.Hubs;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.Vision;

/// <summary>The order of the waiting vision prompts, and moving or cancelling them.</summary>
public sealed class PromptQueueService(StudioDbContext db, PromptGenerationQueue promptQueue, StudioNotifier notifier)
{
    private static readonly PromptGenerationState[] Active = [PromptGenerationState.Running, PromptGenerationState.Queued];

    public async Task<IReadOnlyList<PromptSet>> ListAsync(CancellationToken ct) =>
        await db.PromptSets.AsNoTracking()
            .Where(s => Active.Contains(s.PromptGeneration))
            .OrderBy(s => s.PromptGeneration == PromptGenerationState.Running ? 0 : 1)
            .ThenBy(s => s.PromptQueuePosition)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);

    /// <summary>
    /// The first of <paramref name="count"/> free positions in a row. Clicked-for prompts go before the first waiting
    /// automatic one, which moves back along with all after it; otherwise they go last.
    /// </summary>
    public async Task<long> PositionsForAsync(bool requested, int count, CancellationToken ct)
    {
        var waiting = db.PromptSets.Where(s => s.PromptGeneration == PromptGenerationState.Queued);
        var firstAutomatic = requested
            ? await waiting.Where(s => !s.PromptGenerationRequested).MinAsync(s => (long?)s.PromptQueuePosition, ct)
            : null;
        if (firstAutomatic is not { } position)
            return (await waiting.MaxAsync(s => (long?)s.PromptQueuePosition, ct) ?? 0) + 1;

        await waiting
            .Where(s => s.PromptQueuePosition >= position)
            .ExecuteUpdateAsync(s => s.SetProperty(set => set.PromptQueuePosition, set => set.PromptQueuePosition + count), ct);
        return position;
    }

    /// <summary>Moves a waiting prompt among the waiting ones; the others keep their relative order.</summary>
    public async Task MoveAsync(int setId, QueueMove move, CancellationToken ct)
    {
        var waiting = await db.PromptSets
            .Where(s => s.PromptGeneration == PromptGenerationState.Queued)
            .OrderBy(s => s.PromptQueuePosition)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);
        var index = waiting.FindIndex(s => s.Id == setId);
        if (index < 0)
            throw new UserFacingException("Only waiting prompts can be moved.");
        if (index == 0 && move is QueueMove.Top or QueueMove.Up)
            throw new UserFacingException("This prompt is already first.");
        if (index == waiting.Count - 1 && move is QueueMove.Down or QueueMove.Bottom)
            throw new UserFacingException("This prompt is already last.");

        var target = move switch
        {
            QueueMove.Top => 0,
            QueueMove.Up => index - 1,
            QueueMove.Down => index + 1,
            _ => waiting.Count - 1,
        };
        var set = waiting[index];
        waiting.RemoveAt(index);
        waiting.Insert(target, set);
        for (var position = 0; position < waiting.Count; position++)
            waiting[position].PromptQueuePosition = position + 1;
        await db.SaveChangesAsync(ct);

        foreach (var changed in waiting)
            await notifier.SetUpdated(changed.Id);
    }

    public async Task CancelAsync(int setId, CancellationToken ct)
    {
        var cancelled = await db.PromptSets
            .Where(s => s.Id == setId && Active.Contains(s.PromptGeneration))
            .ExecuteUpdateAsync(s => s
                .SetProperty(set => set.PromptGeneration, PromptGenerationState.None)
                .SetProperty(set => set.ImagesAfterPromptCount, (int?)null)
                .SetProperty(set => set.ImagesAfterPromptResolution, (string?)null)
                .SetProperty(set => set.ImagesAfterPromptOrder, (string?)null), ct);
        if (cancelled == 0)
            throw new UserFacingException("This set has no prompt waiting or running.");

        promptQueue.CancelRunning(setId);
        await notifier.SetUpdated(setId);
    }
}
