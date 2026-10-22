using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Generation;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.DeviantArt;

/// <summary>Fetches the DeviantArt numbers of published sets into one snapshot per set and day.</summary>
public sealed class DeviationStatsCollector(
    StudioDbContext db,
    AppPaths paths,
    DeviantArtApi api,
    DeviantArtAuth auth,
    TimeProvider clock,
    ILogger<DeviationStatsCollector> logger)
{
    public static readonly TimeSpan FreshFor = TimeSpan.FromHours(1);
    // DeviantArt documents no batch limit for metadata, only a "too many deviations" error.
    public const int MetadataBatchSize = 10;
    public const int GalleryPageSize = 24;
    private static readonly SemaphoreSlim Refreshing = new(1, 1);

    public static bool IsStale(DateTimeOffset? fetchedAt, DateTimeOffset now) => fetchedAt is null || now - fetchedAt >= FreshFor;

    /// <returns>Why the numbers could not be fetched, or null when they are fresh.</returns>
    public async Task<string?> RefreshAsync(bool force, CancellationToken ct)
    {
        if (!await auth.IsConnectedAsync(ct))
            return null;

        await Refreshing.WaitAsync(ct);
        try
        {
            var settings = await SettingsStore.LoadAsync(db, paths, ct);
            if (!force && !IsStale(settings.DeviationStatsFetchedAt, clock.GetUtcNow()))
                return null;

            await MatchPublishedDeviationsAsync(ct);
            await FetchSnapshotsAsync(ct);
            settings.DeviationStatsFetchedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (UserFacingException ex)
        {
            logger.LogWarning("Fetching DeviantArt stats failed: {Message}", ex.Message);
            return ex.Message;
        }
        finally
        {
            Refreshing.Release();
        }
    }

    // Sets published before deviation ids were kept only know their URL; the gallery lists both, newest first.
    private async Task MatchPublishedDeviationsAsync(CancellationToken ct)
    {
        var unmatched = await db.PromptSets
            .Where(s => s.PublishedUrl != null && s.PublishedDeviationId == null)
            .ToListAsync(ct);
        var byNumber = unmatched
            .Select(set => (Set: set, Number: DeviationNumberOrNull(set.PublishedUrl!)))
            .Where(entry => entry.Number is not null)
            .GroupBy(entry => entry.Number!)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Set).ToList());
        if (byNumber.Count == 0)
            return;

        var stopBefore = unmatched.Min(set => set.PublishedAt ?? DateTimeOffset.MinValue) - TimeSpan.FromDays(1);
        int? offset = 0;
        while (offset is not null && byNumber.Count > 0)
        {
            var page = await api.SendAsync(
                HttpMethod.Get, $"gallery/all?limit={GalleryPageSize}&offset={offset}&mature_content=true", null, ct);
            var results = page["results"]?.AsArray() ?? [];
            foreach (var deviation in results)
            {
                var url = deviation?["url"]?.GetValue<string>();
                var deviationId = deviation?["deviationid"]?.GetValue<string>();
                if (url is null || deviationId is null || DeviationNumberOrNull(url) is not { } number || !byNumber.Remove(number, out var sets))
                    continue;
                foreach (var set in sets)
                    set.PublishedDeviationId = deviationId;
            }

            var reachedOlderDeviations = PublishedTimeOrNull(results.LastOrDefault()) < stopBefore;
            offset = page["has_more"]?.GetValue<bool>() == true && !reachedOlderDeviations ? page["next_offset"]?.GetValue<int>() : null;
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task FetchSnapshotsAsync(CancellationToken ct)
    {
        var published = await db.PromptSets
            .Where(s => s.PublishedDeviationId != null)
            .Select(s => new { s.Id, DeviationId = s.PublishedDeviationId! })
            .ToListAsync(ct);
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var todays = await db.DeviationStats.Where(s => s.Day == today).ToDictionaryAsync(s => s.PromptSetId, ct);

        foreach (var batch in published.Chunk(MetadataBatchSize))
        {
            var ids = string.Join('&', batch.Select(set => $"deviationids[]={Uri.EscapeDataString(set.DeviationId)}"));
            var answer = await api.SendAsync(HttpMethod.Get, $"deviation/metadata?{ids}&ext_stats=true&mature_content=true", null, ct);
            var setIdsByDeviation = batch.ToDictionary(set => set.DeviationId, set => set.Id, StringComparer.OrdinalIgnoreCase);
            foreach (var metadata in answer["metadata"]?.AsArray() ?? [])
            {
                var stats = metadata?["stats"];
                if (metadata?["deviationid"]?.GetValue<string>() is not { } deviationId || stats is null
                    || !setIdsByDeviation.TryGetValue(deviationId, out var setId))
                    continue;

                if (!todays.TryGetValue(setId, out var snapshot))
                {
                    snapshot = new DeviationStatsSnapshot { PromptSetId = setId, Day = today };
                    db.DeviationStats.Add(snapshot);
                    todays[setId] = snapshot;
                }
                snapshot.FetchedAt = clock.GetUtcNow();
                snapshot.Views = Count(stats["views"]);
                snapshot.Favourites = Count(stats["favourites"]);
                snapshot.Comments = Count(stats["comments"]);
                snapshot.Downloads = Count(stats["downloads"]);
            }
            await db.SaveChangesAsync(ct);
        }
    }

    private static string? DeviationNumberOrNull(string url)
    {
        try
        {
            return DeviationUrl.Parse(url).DeviationId;
        }
        catch (UserFacingException)
        {
            return null;
        }
    }

    // Older API versions sent Unix seconds, newer ones ISO 8601.
    private static DateTimeOffset? PublishedTimeOrNull(JsonNode? deviation)
    {
        var node = deviation?["published_time"];
        var text = node?.GetValueKind() == JsonValueKind.Number ? node.GetValue<long>().ToString(CultureInfo.InvariantCulture) : node?.GetValue<string>();
        if (long.TryParse(text, out var seconds))
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time) ? time : null;
    }

    private static int Count(JsonNode? node) => node?.GetValueKind() == JsonValueKind.Number ? node.GetValue<int>() : 0;
}
