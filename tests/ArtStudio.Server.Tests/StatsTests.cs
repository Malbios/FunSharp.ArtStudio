using System.Net;
using System.Net.Http.Json;
using ArtStudio.Server.Api;
using ArtStudio.Server.Data;
using ArtStudio.Server.DeviantArt;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Stats;
using ArtStudio.Server.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArtStudio.Server.Tests;

public sealed class StatsTests : IDisposable
{
    private readonly StudioAppFactory _factory = new();
    private readonly HttpClient _client;

    public StatsTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private static string DeviationUrl(int number) => $"https://www.deviantart.com/{FakeDeviantArt.Username}/art/Fox-{number}";

    private async Task<int> SeedPublishedSetAsync(string url, string? deviationId, DateTimeOffset publishedAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
        var set = new PromptSet
        {
            Prompt = "fox",
            Resolution = "Native",
            CreatedAt = publishedAt.AddDays(-2),
            ReadyToPostAt = publishedAt.AddDays(-1),
            PublishedAt = publishedAt,
            PublishedUrl = url,
            PublishedDeviationId = deviationId,
        };
        db.PromptSets.Add(set);
        await db.SaveChangesAsync();
        return set.Id;
    }

    private async Task<string?> RefreshAsync(bool force = false)
    {
        var response = await _client.PostAsJsonAsync("/api/stats/deviantart/refresh", new { force });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RefreshStatsDto>())!.Error;
    }

    private async Task<List<DeviationStatsSnapshot>> SnapshotsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<StudioDbContext>().DeviationStats.OrderBy(s => s.PromptSetId).ToListAsync();
    }

    private async Task<string?> PublishedDeviationIdAsync(int setId)
    {
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<StudioDbContext>().PromptSets.SingleAsync(s => s.Id == setId)).PublishedDeviationId;
    }

    [Fact]
    public async Task Refresh_SavesTodaysNumbersPerSet_AndReplacesThemLaterThatDay()
    {
        await _factory.ConnectDeviantArtAsync();
        var fox = await SeedPublishedSetAsync(DeviationUrl(1), "DEV-A", DateTimeOffset.UtcNow.AddDays(-3));
        var owl = await SeedPublishedSetAsync(DeviationUrl(2), "DEV-B", DateTimeOffset.UtcNow.AddDays(-1));
        _factory.DeviantArt.Stats["DEV-A"] = (100, 10, 2, 5);
        _factory.DeviantArt.Stats["DEV-B"] = (40, 4, 1, 0);

        Assert.Null(await RefreshAsync());
        _factory.DeviantArt.Stats["DEV-A"] = (150, 12, 3, 6);
        Assert.Null(await RefreshAsync(force: true));

        var today = DateOnly.FromDateTime(DateTime.Now);
        Assert.Equal(
            [(fox, today, 150, 12, 3, 6), (owl, today, 40, 4, 1, 0)],
            (await SnapshotsAsync()).Select(s => (s.PromptSetId, s.Day, s.Views, s.Favourites, s.Comments, s.Downloads)));
    }

    [Fact]
    public async Task Refresh_WithinAnHour_AsksDeviantArtNothingUnlessForced()
    {
        await _factory.ConnectDeviantArtAsync();
        await SeedPublishedSetAsync(DeviationUrl(1), "DEV-A", DateTimeOffset.UtcNow.AddDays(-3));

        await RefreshAsync();
        await RefreshAsync();
        Assert.Single(_factory.DeviantArt.MetadataRequests);

        await RefreshAsync(force: true);
        Assert.Equal(2, _factory.DeviantArt.MetadataRequests.Count);
    }

    [Fact]
    public async Task Refresh_AsksForTheStatsOfTenDeviationsAtATime()
    {
        await _factory.ConnectDeviantArtAsync();
        for (var number = 1; number <= 12; number++)
            await SeedPublishedSetAsync(DeviationUrl(number), $"DEV-{number}", DateTimeOffset.UtcNow.AddDays(-number));

        await RefreshAsync();

        Assert.Equal(
            [DeviationStatsCollector.MetadataBatchSize, 2],
            _factory.DeviantArt.MetadataRequests.Select(query => query.Split("deviationids[]=").Length - 1));
        Assert.All(_factory.DeviantArt.MetadataRequests, query => Assert.Contains("ext_stats=true", query));
    }

    private void AddDailyOwnDeviations(int count)
    {
        for (var daysAgo = 0; daysAgo < count; daysAgo++)
            _factory.DeviantArt.OwnDeviations.Add(($"GAL-{daysAgo}", DeviationUrl(1000 + daysAgo), DateTimeOffset.UtcNow.AddDays(-daysAgo)));
    }

    [Fact]
    public async Task Refresh_MatchesSetsPublishedWithoutAnIdThroughTheGallery()
    {
        await _factory.ConnectDeviantArtAsync();
        AddDailyOwnDeviations(60);
        var setId = await SeedPublishedSetAsync(DeviationUrl(1030), null, DateTimeOffset.UtcNow.AddDays(-30));
        _factory.DeviantArt.Stats["GAL-30"] = (7, 1, 0, 0);

        await RefreshAsync();

        Assert.Equal("GAL-30", await PublishedDeviationIdAsync(setId));
        Assert.Equal(2, _factory.DeviantArt.GalleryAllRequests.Count);
        Assert.Equal(7, Assert.Single(await SnapshotsAsync()).Views);
    }

    [Fact]
    public async Task Refresh_StopsSearchingTheGalleryOnceItReachesOlderDeviations()
    {
        await _factory.ConnectDeviantArtAsync();
        AddDailyOwnDeviations(60);
        var setId = await SeedPublishedSetAsync(DeviationUrl(5), null, DateTimeOffset.UtcNow.AddDays(-2));

        Assert.Null(await RefreshAsync());

        Assert.Null(await PublishedDeviationIdAsync(setId));
        Assert.Single(_factory.DeviantArt.GalleryAllRequests);
    }

    [Fact]
    public async Task Refresh_WhenDeviantArtLimitsRequests_KeepsEarlierNumbers_AndSaysSo()
    {
        await _factory.ConnectDeviantArtAsync();
        await SeedPublishedSetAsync(DeviationUrl(1), "DEV-A", DateTimeOffset.UtcNow.AddDays(-3));
        _factory.DeviantArt.Stats["DEV-A"] = (100, 10, 2, 5);
        await RefreshAsync();

        _factory.DeviantArt.MetadataFailure = (HttpStatusCode.TooManyRequests, """{ "error": "rate_limited" }""");
        var error = await RefreshAsync(force: true);

        Assert.Contains("limiting requests", error);
        Assert.Equal(100, Assert.Single(await SnapshotsAsync()).Views);
    }

    private async Task<StatsDto> StatsAsync() => (await _client.GetFromJsonAsync<StatsDto>("/api/stats", StudioAppFactory.Json))!;

    private async Task SeedAsync(Action<StudioDbContext> seed)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    private Task AddSnapshotAsync(int setId, int daysAgo, int views, int favourites, int comments = 0, int downloads = 0) =>
        SeedAsync(db => db.DeviationStats.Add(new DeviationStatsSnapshot
        {
            PromptSetId = setId, Day = Today.AddDays(-daysAgo), FetchedAt = DateTimeOffset.UtcNow,
            Views = views, Favourites = favourites, Comments = comments, Downloads = downloads,
        }));

    /// <summary>A set with one completed job that took 10 seconds per image (none without images), and the given images picked in order.</summary>
    private async Task<int> SeedSetAsync(string prompt, int images, int[] picked, Action<PromptSet>? configure = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
        var now = DateTimeOffset.UtcNow;
        var job = new GenerationJob
        {
            Prompt = prompt, Resolution = "Native", RequestedCount = images, CompletedCount = images, Status = JobStatus.Completed,
            CreatedAt = now, StartedAt = now.AddSeconds(-10 * images), FinishedAt = now,
        };
        var set = new PromptSet { Prompt = prompt, Resolution = "Native", CreatedAt = now, Jobs = images > 0 ? [job] : [] };
        configure?.Invoke(set);
        db.PromptSets.Add(set);
        await db.SaveChangesAsync();

        var generated = Enumerable.Range(0, images)
            .Select(index => new GeneratedImage { PromptSetId = set.Id, GenerationJobId = job.Id, FilePath = $"{set.Id}-{index}.png", CreatedAt = now })
            .ToList();
        db.Images.AddRange(generated);
        await db.SaveChangesAsync();
        db.Picks.AddRange(picked.Select((index, position) =>
            new PickedImage { PromptSetId = set.Id, GeneratedImageId = generated[index].Id, Position = position }));
        await db.SaveChangesAsync();
        return set.Id;
    }

    private static Action<PromptSet> Published(int daysAgo, string? deviationId = null) => set =>
    {
        set.CreatedAt = DateTimeOffset.UtcNow.AddDays(-daysAgo - 2);
        set.ReadyToPostAt = DateTimeOffset.UtcNow.AddDays(-daysAgo - 1);
        set.PublishedAt = DateTimeOffset.UtcNow.AddDays(-daysAgo);
        set.PublishedUrl = DeviationUrl(set.Prompt.Length);
        set.PublishedDeviationId = deviationId;
        set.PostTitle = set.Prompt;
    };

    [Fact]
    public async Task Stats_ShowEachDeviationsNumbers_GainsAndRates()
    {
        var fox = await SeedSetAsync("fox", 1, [0], Published(daysAgo: 10));
        var owl = await SeedSetAsync("owl", 1, [0], Published(daysAgo: 2));
        await AddSnapshotAsync(fox, daysAgo: 8, views: 100, favourites: 5);
        await AddSnapshotAsync(fox, daysAgo: 7, views: 120, favourites: 6);
        await AddSnapshotAsync(fox, daysAgo: 0, views: 220, favourites: 16, comments: 3, downloads: 4);
        await AddSnapshotAsync(owl, daysAgo: 0, views: 30, favourites: 3);

        var stats = (await StatsAsync()).DeviantArt;

        Assert.Equal(new DeviantArtTotalsDto(2, 2, 250, 19, 3, 4, ViewsLast7Days: 130, FavouritesLast7Days: 13), stats.Totals);
        Assert.Equal(Today.AddDays(-8), stats.HistorySince);
        Assert.Equal([owl, fox], stats.Deviations.Select(d => d.SetId));
        var foxStats = stats.Deviations[1];
        Assert.Equal(("fox", 220, 16, 3, 4, 100, 10), (foxStats.Title, foxStats.Views, foxStats.Favourites, foxStats.Comments,
            foxStats.Downloads, foxStats.ViewsLast7Days, foxStats.FavouritesLast7Days));
        Assert.Equal(100.0 * 16 / 220, foxStats.FavouritesPer100Views!.Value, 6);
        Assert.Equal(10, foxStats.DaysOnline, 2);
        Assert.Equal(22, foxStats.ViewsPerDay!.Value, 1);

        Assert.Equal(StatsService.ChartDays, stats.Daily.Count);
        Assert.Equal((Today, 100, 10), (stats.Daily[^1].Day, stats.Daily[^1].Views, stats.Daily[^1].Favourites));
        Assert.Equal((20, 1), (stats.Daily[^8].Views, stats.Daily[^8].Favourites));
        Assert.Equal(0, stats.Daily[^9].Views);
    }

    [Fact]
    public async Task Stats_CreditEachDeviationToThePresetOfItsFirstPick()
    {
        await SeedAsync(db => db.BuildingBlocks.AddRange(
            new BuildingBlock { Kind = BuildingBlockKind.Preset, Label = "Watercolor", Text = "", ArtStyle = "soft watercolor", SortOrder = 1 },
            new BuildingBlock { Kind = BuildingBlockKind.Preset, Label = "Ink", Text = "", ArtStyle = "ink lines", SortOrder = 2 },
            new BuildingBlock { Kind = BuildingBlockKind.Preset, Label = "Unused", Text = "", ArtStyle = "pixel art", SortOrder = 3 }));
        var fox = await SeedSetAsync("a fox, soft watercolor", 4, [0, 1], Published(daysAgo: 3));
        await SeedSetAsync("an owl, ink lines", 2, []);
        await SeedSetAsync("a cat", 2, [0]);
        await AddSnapshotAsync(fox, daysAgo: 0, views: 100, favourites: 10);

        var stats = await StatsAsync();

        Assert.Equal(
            [("Watercolor", 4, 2, 0.5, 1, 100.0, 10.0), ("Ink", 2, 0, 0.0, 0, null, null), (StatsService.NoPresetName, 2, 1, 0.5, 0, null, null)],
            stats.Presets.Select(p => (p.Name, p.Images, p.Picks, p.PickRate!.Value, p.Published, p.AverageViews, p.AverageFavourites)));
        Assert.Equal("Watercolor", Assert.Single(stats.DeviantArt.Deviations).Preset);
    }

    [Fact]
    public async Task Stats_CountTheStudioPipeline()
    {
        await SeedSetAsync("draft", 0, [], set => set.IsDraft = true);
        var working = await SeedSetAsync("working", 4, []);
        await SeedSetAsync("post", 4, [0], set => { set.CreatedAt = DateTimeOffset.UtcNow.AddDays(-3); set.ReadyToPostAt = DateTimeOffset.UtcNow; });
        await SeedSetAsync("stashed", 4, [0, 1], set =>
        {
            set.CreatedAt = DateTimeOffset.UtcNow.AddDays(-1);
            set.ReadyToPostAt = DateTimeOffset.UtcNow;
            set.StashItemId = 1;
        });
        await SeedSetAsync("published", 4, [0], Published(daysAgo: 1));
        await SeedSetAsync("archived", 4, [], set => set.ArchivedAt = DateTimeOffset.UtcNow);
        await SeedAsync(db => db.Jobs.Add(new GenerationJob
        {
            PromptSetId = working, Prompt = "working", Resolution = "Native", RequestedCount = 1, Status = JobStatus.Failed,
            CreatedAt = DateTimeOffset.UtcNow, FinishedAt = DateTimeOffset.UtcNow,
        }));

        var pipeline = (await StatsAsync()).Pipeline;

        Assert.Equal(new StageCountsDto(Drafts: 1, Working: 1, Post: 1, Stashed: 1, Published: 1, Archived: 1), pipeline.Stages);
        Assert.Equal((20, 20, 20), (pipeline.Images, pipeline.ImagesLast7Days, pipeline.ImagesLast30Days));
        Assert.Equal(20, pipeline.ImagesPerDay[^1].Count);
        Assert.Equal(4.0 / 12, pipeline.PickRate!.Value, 6);
        Assert.Equal(10, pipeline.SecondsPerImage!.Value, 3);
        Assert.Equal((5, 1), (pipeline.JobsCompletedLast30Days, pipeline.JobsFailedLast30Days));
        Assert.Equal(1, pipeline.MedianDaysToReady!.Value, 2);
        Assert.Equal(1, pipeline.MedianDaysReadyToPublished!.Value, 2);
    }

    [Fact]
    public async Task Stats_RankTheArtistsWhoInspiredTheMostSets()
    {
        var published = await SeedSetAsync("fox", 1, [0], set => { Published(daysAgo: 2)(set); set.DeviantArtAuthor = "alice"; set.SourceKind = SourceKind.DeviantArt; });
        await SeedSetAsync("owl", 1, [], set => { set.DeviantArtAuthor = "Alice"; set.SourceKind = SourceKind.DeviantArt; });
        await SeedSetAsync("cat", 1, [], set => { set.DeviantArtAuthor = "bob"; set.SourceKind = SourceKind.DeviantArt; });
        await SeedSetAsync("pasted", 1, [], set => set.SourceKind = SourceKind.Paste);
        await AddSnapshotAsync(published, daysAgo: 0, views: 50, favourites: 8);

        var inspiration = (await StatsAsync()).Inspiration;

        Assert.Equal([("alice", 2, 1, 8.0), ("bob", 1, 0, null)],
            inspiration.TopArtists.Select(a => (a.Username, a.Sets, a.Published, a.AverageFavourites)));
        Assert.Equal([(SourceKind.DeviantArt, 3), (SourceKind.Paste, 1)], inspiration.Sources.Select(s => (s.Source, s.Sets)));
    }

    [Fact]
    public async Task Stats_AreStaleUntilFetched_OnlyWhenConnectedWithPublishedSets()
    {
        await SeedPublishedSetAsync(DeviationUrl(1), "DEV-A", DateTimeOffset.UtcNow.AddDays(-3));
        Assert.False((await StatsAsync()).DeviantArt.Stale);

        await _factory.ConnectDeviantArtAsync();
        Assert.True((await StatsAsync()).DeviantArt.Stale);

        await RefreshAsync();
        var stats = (await StatsAsync()).DeviantArt;
        Assert.False(stats.Stale);
        Assert.NotNull(stats.FetchedAt);
    }

    [Fact]
    public async Task Refresh_WithoutConnection_DoesNothing()
    {
        await SeedPublishedSetAsync(DeviationUrl(1), "DEV-A", DateTimeOffset.UtcNow.AddDays(-3));

        Assert.Null(await RefreshAsync());

        Assert.Empty(_factory.DeviantArt.MetadataRequests);
        Assert.Empty(await SnapshotsAsync());
    }
}
