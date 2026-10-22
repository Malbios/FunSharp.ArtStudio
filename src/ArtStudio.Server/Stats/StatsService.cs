using ArtStudio.Server.Api;
using ArtStudio.Server.Data;
using ArtStudio.Server.DeviantArt;
using ArtStudio.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.Stats;

/// <summary>Works out the dashboard from what is stored; it never calls DeviantArt itself.</summary>
public sealed class StatsService(StudioDbContext db, DeviantArtAuth auth, TimeProvider clock)
{
    public const int TrendDays = 7;
    public const int ChartDays = 30;
    public const int TopArtistCount = 10;
    public const string NoPresetName = "No preset";

    private sealed record SetRow(
        int Id, bool IsDraft, SourceKind Source, string? Author, DateTimeOffset CreatedAt, DateTimeOffset? ReadyToPostAt,
        bool Stashed, DateTimeOffset? ArchivedAt, string? Title, string? PublishedUrl, DateTimeOffset? PublishedAt);

    private sealed record JobRow(int Id, string Prompt, JobStatus Status, int CompletedCount, DateTimeOffset CreatedAt,
        DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt);

    private sealed record ImageRow(int Id, int SetId, int JobId, DateTimeOffset CreatedAt);

    private sealed record PickRow(int SetId, int ImageId, int Position);

    private sealed record SnapshotRow(int SetId, DateOnly Day, int Views, int Favourites, int Comments, int Downloads);

    private sealed record PresetRow(int Id, string Label, string ArtStyle);

    private sealed record Deviation(SetRow Set, int? PresetId, DeviationStatsDto Dto);

    public async Task<StatsDto> GetAsync(CancellationToken ct)
    {
        var sets = await db.PromptSets
            .Select(s => new SetRow(s.Id, s.IsDraft, s.SourceKind, s.DeviantArtAuthor, s.CreatedAt, s.ReadyToPostAt,
                s.StashItemId != null, s.ArchivedAt, s.PostTitle, s.PublishedUrl, s.PublishedAt))
            .ToListAsync(ct);
        var jobs = await db.Jobs
            .Select(j => new JobRow(j.Id, j.Prompt, j.Status, j.CompletedCount, j.CreatedAt, j.StartedAt, j.FinishedAt))
            .ToListAsync(ct);
        var images = await db.Images.Select(i => new ImageRow(i.Id, i.PromptSetId, i.GenerationJobId, i.CreatedAt)).ToListAsync(ct);
        var picks = await db.Picks.Select(p => new PickRow(p.PromptSetId, p.GeneratedImageId, p.Position)).ToListAsync(ct);
        var snapshots = await db.DeviationStats
            .Select(s => new SnapshotRow(s.PromptSetId, s.Day, s.Views, s.Favourites, s.Comments, s.Downloads))
            .ToListAsync(ct);
        var presets = await db.BuildingBlocks
            .Where(b => b.Kind == BuildingBlockKind.Preset && b.ArtStyle != null && b.ArtStyle != "")
            .OrderBy(b => b.SortOrder)
            .Select(b => new PresetRow(b.Id, b.Label, b.ArtStyle!))
            .ToListAsync(ct);
        var fetchedAt = await db.Settings.Select(s => s.DeviationStatsFetchedAt).FirstOrDefaultAsync(ct);
        var connected = await auth.IsConnectedAsync(ct);

        var now = clock.GetUtcNow();
        var presetByJob = jobs.ToDictionary(job => job.Id, job => PresetOf(job.Prompt, presets)?.Id);
        var presetByImage = images.ToDictionary(image => image.Id, image => presetByJob.GetValueOrDefault(image.JobId));
        var mainPickBySet = picks.GroupBy(p => p.SetId).ToDictionary(group => group.Key, group => group.MinBy(p => p.Position)!.ImageId);
        var deviations = Deviations(sets, snapshots, mainPickBySet, presetByImage, presets, now);

        return new StatsDto(
            DeviantArtStats(connected, fetchedAt, deviations, snapshots, now),
            PresetStats(presets, images, picks, presetByImage, deviations),
            PipelineStats(sets, jobs, images, picks, now),
            InspirationStats(sets, deviations));
    }

    // Jobs don't record their preset, only the prompt its art style was written into; the longest match is the most specific.
    private static PresetRow? PresetOf(string prompt, IReadOnlyList<PresetRow> presets) =>
        presets.Where(p => prompt.Contains(p.ArtStyle, StringComparison.Ordinal)).MaxBy(p => p.ArtStyle.Length);

    private DateOnly LocalDay(DateTimeOffset time) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time, clock.LocalTimeZone).DateTime);

    private List<Deviation> Deviations(
        List<SetRow> sets, List<SnapshotRow> snapshots, Dictionary<int, int> mainPickBySet, Dictionary<int, int?> presetByImage,
        List<PresetRow> presets, DateTimeOffset now)
    {
        var snapshotsBySet = snapshots.GroupBy(s => s.SetId).ToDictionary(group => group.Key, group => group.OrderBy(s => s.Day).ToList());
        var presetNames = presets.ToDictionary(p => p.Id, p => p.Label);
        var cutoff = LocalDay(now).AddDays(-TrendDays);

        return sets
            .Where(set => set.PublishedAt is not null && set.PublishedUrl is not null)
            .OrderByDescending(set => set.PublishedAt)
            .Select(set =>
            {
                int? mainPick = mainPickBySet.TryGetValue(set.Id, out var imageId) ? imageId : null;
                var presetId = mainPick is int id ? presetByImage.GetValueOrDefault(id) : null;
                var daysOnline = Math.Max(0, (now - set.PublishedAt!.Value).TotalDays);
                var history = snapshotsBySet.GetValueOrDefault(set.Id);
                var latest = history?.Last();
                var baseline = LocalDay(set.PublishedAt.Value) > cutoff
                    ? null
                    : history?.LastOrDefault(s => s.Day <= cutoff) ?? history?.First();
                var dto = new DeviationStatsDto(
                    set.Id,
                    set.Title,
                    set.PublishedUrl!,
                    mainPick is int image ? ApiUrls.Image(image) : null,
                    set.PublishedAt.Value,
                    daysOnline,
                    presetId is int preset ? presetNames[preset] : null,
                    latest?.Views,
                    latest?.Favourites,
                    latest?.Comments,
                    latest?.Downloads,
                    latest is { Views: > 0 } ? 100.0 * latest.Favourites / latest.Views : null,
                    latest is null ? null : latest.Views / Math.Max(1, daysOnline),
                    latest is null ? null : latest.Views - (baseline?.Views ?? 0),
                    latest is null ? null : latest.Favourites - (baseline?.Favourites ?? 0));
                return new Deviation(set, presetId, dto);
            })
            .ToList();
    }

    private DeviantArtStatsDto DeviantArtStats(
        bool connected, DateTimeOffset? fetchedAt, List<Deviation> deviations, List<SnapshotRow> snapshots, DateTimeOffset now)
    {
        var tracked = deviations.Select(d => d.Dto).Where(d => d.Views is not null).ToList();
        var totals = new DeviantArtTotalsDto(
            deviations.Count,
            tracked.Count,
            tracked.Sum(d => d.Views!.Value),
            tracked.Sum(d => d.Favourites!.Value),
            tracked.Sum(d => d.Comments!.Value),
            tracked.Sum(d => d.Downloads!.Value),
            tracked.Sum(d => d.ViewsLast7Days!.Value),
            tracked.Sum(d => d.FavouritesLast7Days!.Value));

        return new DeviantArtStatsDto(
            connected,
            fetchedAt,
            connected && deviations.Count > 0 && DeviationStatsCollector.IsStale(fetchedAt, now),
            snapshots.Count == 0 ? null : snapshots.Min(s => s.Day),
            totals,
            deviations.Select(d => d.Dto).ToList(),
            DailyGains(deviations, snapshots, now));
    }

    // A day's gain is what the numbers grew since the day before; a deviation first counts from its first snapshot,
    // and only gains its whole count then when it was new, so tracking an old deviation adds no spike.
    private List<DailyGainDto> DailyGains(List<Deviation> deviations, List<SnapshotRow> snapshots, DateTimeOffset now)
    {
        var today = LocalDay(now);
        var days = Enumerable.Range(0, ChartDays).Select(offset => today.AddDays(offset - ChartDays + 1)).ToList();
        var views = new int[ChartDays];
        var favourites = new int[ChartDays];

        foreach (var deviation in deviations)
        {
            var history = snapshots.Where(s => s.SetId == deviation.Set.Id).OrderBy(s => s.Day).ToList();
            if (history.Count == 0)
                continue;
            var publishedDay = LocalDay(deviation.Set.PublishedAt!.Value);
            for (var index = 0; index < ChartDays; index++)
            {
                var day = days[index];
                var current = history.LastOrDefault(s => s.Day <= day);
                if (current is null)
                    continue;
                var previous = history.LastOrDefault(s => s.Day <= day.AddDays(-1));
                if (previous is null && (current.Day != day || publishedDay < day.AddDays(-1)))
                    continue;
                views[index] += current.Views - (previous?.Views ?? 0);
                favourites[index] += current.Favourites - (previous?.Favourites ?? 0);
            }
        }

        return days.Select((day, index) => new DailyGainDto(day, views[index], favourites[index])).ToList();
    }

    private static List<PresetStatsDto> PresetStats(
        List<PresetRow> presets, List<ImageRow> images, List<PickRow> picks, Dictionary<int, int?> presetByImage, List<Deviation> deviations)
    {
        var rows = presets.Select(p => (Id: (int?)p.Id, Name: p.Label)).Append((Id: null, Name: NoPresetName));
        return rows
            .Select(row =>
            {
                var imageCount = images.Count(image => presetByImage[image.Id] == row.Id);
                var pickCount = picks.Count(pick => presetByImage.GetValueOrDefault(pick.ImageId) == row.Id);
                var published = deviations.Where(d => d.PresetId == row.Id).ToList();
                var tracked = published.Where(d => d.Dto.Views is not null).ToList();
                return new PresetStatsDto(
                    row.Id,
                    row.Name,
                    imageCount,
                    pickCount,
                    imageCount == 0 ? null : (double)pickCount / imageCount,
                    published.Count,
                    tracked.Count == 0 ? null : tracked.Average(d => d.Dto.Views!.Value),
                    tracked.Count == 0 ? null : tracked.Average(d => d.Dto.Favourites!.Value));
            })
            .Where(row => row.Images > 0 || row.Published > 0)
            .ToList();
    }

    private PipelineStatsDto PipelineStats(List<SetRow> sets, List<JobRow> jobs, List<ImageRow> images, List<PickRow> picks, DateTimeOffset now)
    {
        var inPost = sets.Where(s => s.ReadyToPostAt is not null && s.PublishedAt is null && s.ArchivedAt is null).ToList();
        var stages = new StageCountsDto(
            sets.Count(s => s.IsDraft),
            sets.Count(s => !s.IsDraft && s.ReadyToPostAt is null && s.ArchivedAt is null),
            inPost.Count(s => !s.Stashed),
            inPost.Count(s => s.Stashed),
            sets.Count(s => s.PublishedAt is not null && s.ArchivedAt is null),
            sets.Count(s => s.ArchivedAt is not null));

        var today = LocalDay(now);
        var imagesPerDay = images.GroupBy(image => LocalDay(image.CreatedAt)).ToDictionary(group => group.Key, group => group.Count());
        var chart = Enumerable.Range(0, ChartDays)
            .Select(offset => today.AddDays(offset - ChartDays + 1))
            .Select(day => new DailyCountDto(day, imagesPerDay.GetValueOrDefault(day)))
            .ToList();

        // Only sets that reached Post are done being picked from.
        var pickedFrom = sets.Where(s => s.ReadyToPostAt is not null).Select(s => s.Id).ToHashSet();
        var imagesPickedFrom = images.Count(image => pickedFrom.Contains(image.SetId));
        var picksMade = picks.Count(pick => pickedFrom.Contains(pick.SetId));

        var monthAgo = now.AddDays(-ChartDays);
        var recentJobs = jobs.Where(job => (job.FinishedAt ?? job.CreatedAt) >= monthAgo).ToList();
        var timed = recentJobs
            .Where(job => job.Status == JobStatus.Completed && job.StartedAt is not null && job.FinishedAt is not null && job.CompletedCount > 0)
            .ToList();
        var renderedImages = timed.Sum(job => job.CompletedCount);

        return new PipelineStatsDto(
            stages,
            images.Count,
            images.Count(image => image.CreatedAt >= now.AddDays(-TrendDays)),
            images.Count(image => image.CreatedAt >= monthAgo),
            chart,
            imagesPickedFrom == 0 ? null : (double)picksMade / imagesPickedFrom,
            renderedImages == 0 ? null : timed.Sum(job => (job.FinishedAt!.Value - job.StartedAt!.Value).TotalSeconds) / renderedImages,
            recentJobs.Count(job => job.Status == JobStatus.Completed),
            recentJobs.Count(job => job.Status == JobStatus.Failed),
            Median(sets.Where(s => s.ReadyToPostAt is not null).Select(s => (s.ReadyToPostAt!.Value - s.CreatedAt).TotalDays)),
            Median(sets.Where(s => s.ReadyToPostAt is not null && s.PublishedAt is not null)
                .Select(s => (s.PublishedAt!.Value - s.ReadyToPostAt!.Value).TotalDays)));
    }

    private static InspirationStatsDto InspirationStats(List<SetRow> sets, List<Deviation> deviations)
    {
        var sources = sets.GroupBy(s => s.Source)
            .Select(group => new SourceCountDto(group.Key, group.Count()))
            .OrderByDescending(source => source.Sets)
            .ToList();
        var publishedBySet = deviations.ToDictionary(d => d.Set.Id);
        var artists = sets.Where(s => !string.IsNullOrEmpty(s.Author))
            .GroupBy(s => s.Author!, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var published = group.Where(s => publishedBySet.ContainsKey(s.Id)).Select(s => publishedBySet[s.Id].Dto).ToList();
                var tracked = published.Where(d => d.Favourites is not null).ToList();
                return new InspiringArtistDto(
                    group.First().Author!,
                    group.Count(),
                    published.Count,
                    tracked.Count == 0 ? null : tracked.Average(d => d.Favourites!.Value));
            })
            .OrderByDescending(artist => artist.Sets)
            .ThenBy(artist => artist.Username, StringComparer.OrdinalIgnoreCase)
            .Take(TopArtistCount)
            .ToList();
        return new InspirationStatsDto(sources, artists);
    }

    private static double? Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToList();
        if (sorted.Count == 0)
            return null;
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }
}
