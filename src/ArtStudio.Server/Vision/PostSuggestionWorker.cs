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

public sealed class PostSuggestionWorker(
    IServiceScopeFactory scopeFactory,
    PostSuggestionQueue queue,
    StudioNotifier notifier,
    ILogger<PostSuggestionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
        }
    }

    private async Task SuggestAsync(int setId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<StudioDbContext>();
        var set = await db.PromptSets.AsNoTracking().SingleOrDefaultAsync(s => s.Id == setId, ct);
        if (set is null || set.ReadyToPostAt is null || set.ArchivedAt is not null || set.StashItemId is not null)
            return;

        var title = PostSuggestionQueue.NeedsTitle(set)
            ? await TryAsync("title", setId, () => services.GetRequiredService<TitleSuggester>().SuggestAsync(setId, ct))
            : null;
        var tags = PostSuggestionQueue.NeedsTags(set)
            ? await TryAsync("tags", setId, () => services.GetRequiredService<TagSuggester>().SuggestAsync(setId, ct))
            : null;

        var current = await db.PromptSets.SingleAsync(s => s.Id == setId, ct);
        if (title is { Length: > 0 } && PostSuggestionQueue.NeedsTitle(current))
            current.PostTitle = title;
        if (tags is { Count: > 0 } && PostSuggestionQueue.NeedsTags(current))
            current.PostTags = string.Join(' ', TagSuggester.DefaultTags.Concat(tags));
        await db.SaveChangesAsync(ct);
    }

    private async Task<T?> TryAsync<T>(string what, int setId, Func<Task<T>> suggest) where T : class
    {
        try
        {
            return await suggest();
        }
        catch (UserFacingException ex)
        {
            logger.LogWarning("No {What} suggested for set {SetId}: {Reason}", what, setId, ex.Message);
            return null;
        }
    }
}
