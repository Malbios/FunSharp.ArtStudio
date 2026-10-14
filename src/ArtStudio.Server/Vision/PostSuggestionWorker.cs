using System.Threading.Channels;
using ArtStudio.Server.Data;
using ArtStudio.Server.DeviantArt;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Generation;
using ArtStudio.Server.Hubs;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.Vision;

/// <summary>Sets waiting for a suggested title and tags. Kept in memory: after a restart the suggest buttons are the fallback.</summary>
public sealed class PostSuggestionQueue
{
    private readonly Channel<int> _setIds = Channel.CreateUnbounded<int>();
    private readonly HashSet<int> _suggesting = [];
    private readonly Lock _lock = new();

    public void Enqueue(int setId)
    {
        lock (_lock)
        {
            if (!_suggesting.Add(setId))
                return;
        }
        _setIds.Writer.TryWrite(setId);
    }

    public bool IsSuggesting(int setId)
    {
        lock (_lock)
            return _suggesting.Contains(setId);
    }

    public ValueTask<int> NextAsync(CancellationToken ct) => _setIds.Reader.ReadAsync(ct);

    public void Done(int setId)
    {
        lock (_lock)
            _suggesting.Remove(setId);
    }

    public static bool NeedsTitle(PromptSet set) => string.IsNullOrWhiteSpace(set.PostTitle);

    public static bool NeedsTags(PromptSet set) =>
        (set.PostTags ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .All(tag => TagSuggester.DefaultTags.Contains(tag.TrimStart('#').ToLowerInvariant()));

    public static bool NeedsSuggestions(PromptSet set) => NeedsTitle(set) || NeedsTags(set);
}

/// <summary>
/// Lets one title or tag suggestion run at a time. A clicked suggestion waits only for the one already running,
/// ahead of the background worker's next step.
/// </summary>
public sealed class PostSuggestionTurns
{
    private readonly Lock _lock = new();
    private readonly LinkedList<TaskCompletionSource> _clicked = [];
    private readonly LinkedList<TaskCompletionSource> _background = [];
    private bool _busy;

    public Task<IDisposable> ClickedAsync(CancellationToken ct) => WaitAsync(_clicked, ct);

    public Task<IDisposable> BackgroundAsync(CancellationToken ct) => WaitAsync(_background, ct);

    private async Task<IDisposable> WaitAsync(LinkedList<TaskCompletionSource> line, CancellationToken ct)
    {
        var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        LinkedListNode<TaskCompletionSource> place;
        lock (_lock)
        {
            if (!_busy)
            {
                _busy = true;
                return new Turn(this);
            }
            place = line.AddLast(waiter);
        }

        await using (ct.Register(() => LeaveLine(line, place)))
            await waiter.Task;
        return new Turn(this);
    }

    private void LeaveLine(LinkedList<TaskCompletionSource> line, LinkedListNode<TaskCompletionSource> place)
    {
        lock (_lock)
        {
            // Already handed the turn: the caller still gets it and gives it back when disposing.
            if (place.List is null)
                return;
            line.Remove(place);
        }
        place.Value.TrySetCanceled();
    }

    private void PassOn()
    {
        TaskCompletionSource? next;
        lock (_lock)
        {
            var line = _clicked.Count > 0 ? _clicked : _background;
            next = line.First?.Value;
            if (next is null)
                _busy = false;
            else
                line.RemoveFirst();
        }
        next?.TrySetResult();
    }

    private sealed class Turn(PostSuggestionTurns turns) : IDisposable
    {
        private int _done;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0)
                turns.PassOn();
        }
    }
}

public sealed record PostSuggestionTiming(TimeSpan FirstRetryDelay, TimeSpan MaxRetryDelay, TimeSpan PauseBetweenSets)
{
    public static readonly PostSuggestionTiming Default = new(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(16), TimeSpan.FromSeconds(5));
}

public sealed class PostSuggestionWorker(
    IServiceScopeFactory scopeFactory,
    PostSuggestionQueue queue,
    PostSuggestionTurns turns,
    PostSuggestionTiming timing,
    StudioNotifier notifier,
    ILogger<PostSuggestionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await QueueReadySetsMissingSuggestionsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            int setId;
            try
            {
                setId = await queue.NextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await SuggestAsync(setId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Suggesting a title and tags for set {SetId} failed", setId);
            }
            finally
            {
                queue.Done(setId);
                await notifier.SetUpdated(setId);
            }

            try
            {
                // Each set costs up to 20 DeviantArt tag checks; back to back they run into its rate limit.
                await Task.Delay(timing.PauseBetweenSets, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    // Covers sets that became ready before this feature existed and suggestions a restart interrupted.
    private async Task QueueReadySetsMissingSuggestionsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var readySets = await scope.ServiceProvider.GetRequiredService<StudioDbContext>().PromptSets.AsNoTracking()
            .Where(s => s.ReadyToPostAt != null && s.ArchivedAt == null && s.StashItemId == null)
            .OrderBy(s => s.ReadyToPostAt).ThenBy(s => s.Id)
            .ToListAsync(ct);
        foreach (var set in readySets.Where(PostSuggestionQueue.NeedsSuggestions))
            queue.Enqueue(set.Id);
    }

    private async Task SuggestAsync(int setId, CancellationToken ct)
    {
        var set = await StillWaitingAsync(setId, ct);
        if (set is null)
            return;

        if (PostSuggestionQueue.NeedsTitle(set))
        {
            var title = await TryAsync("title", setId, services => services.GetRequiredService<TitleSuggester>().SuggestAsync(setId, ct), ct);
            if (title is { Length: > 0 })
                await SaveAsync(setId, PostSuggestionQueue.NeedsTitle, current => current.PostTitle = title, ct);
        }

        if (PostSuggestionQueue.NeedsTags(set))
        {
            var tags = await SuggestTagsAsync(setId, ct);
            if (tags is { Count: > 0 })
                await SaveAsync(setId, PostSuggestionQueue.NeedsTags,
                    current => current.PostTags = string.Join(' ', TagSuggester.DefaultTags.Concat(tags)), ct);
        }
    }

    private async Task<IReadOnlyList<string>?> SuggestTagsAsync(int setId, CancellationToken ct)
    {
        var delay = timing.FirstRetryDelay;
        while (true)
        {
            try
            {
                using var turn = await turns.BackgroundAsync(ct);
                using var scope = scopeFactory.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<TagSuggester>().SuggestAsync(setId, ct);
            }
            catch (DeviantArtRateLimitedException)
            {
                logger.LogWarning("DeviantArt is limiting requests; retrying the tags for set {SetId} in {Delay}", setId, delay);
                await Task.Delay(delay, ct);
                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, timing.MaxRetryDelay.Ticks));
                if (await StillWaitingAsync(setId, ct) is not { } set || !PostSuggestionQueue.NeedsTags(set))
                    return null;
            }
            catch (UserFacingException ex)
            {
                logger.LogWarning("No tags suggested for set {SetId}: {Reason}", setId, ex.Message);
                return null;
            }
        }
    }

    /// <summary>The set, unless it left Post or went to the Stash, after which suggestions are no use.</summary>
    private async Task<PromptSet?> StillWaitingAsync(int setId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var set = await scope.ServiceProvider.GetRequiredService<StudioDbContext>().PromptSets.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == setId, ct);
        return set is { ReadyToPostAt: not null, ArchivedAt: null, StashItemId: null } ? set : null;
    }

    // Loads the set afresh, so a title or tags entered meanwhile are never overwritten.
    private async Task SaveAsync(int setId, Func<PromptSet, bool> stillNeeded, Action<PromptSet> apply, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
        var current = await db.PromptSets.SingleAsync(s => s.Id == setId, ct);
        if (!stillNeeded(current))
            return;
        apply(current);
        await db.SaveChangesAsync(ct);
        await notifier.SetUpdated(setId);
    }

    private async Task<T?> TryAsync<T>(string what, int setId, Func<IServiceProvider, Task<T>> suggest, CancellationToken ct) where T : class
    {
        try
        {
            using var turn = await turns.BackgroundAsync(ct);
            using var scope = scopeFactory.CreateScope();
            return await suggest(scope.ServiceProvider);
        }
        catch (UserFacingException ex)
        {
            logger.LogWarning("No {What} suggested for set {SetId}: {Reason}", what, setId, ex.Message);
            return null;
        }
    }
}
