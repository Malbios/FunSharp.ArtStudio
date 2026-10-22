using System.Net;
using System.Net.Http.Json;
using ArtStudio.Server.Api;
using ArtStudio.Server.Data;
using ArtStudio.Server.DeviantArt;
using ArtStudio.Server.Domain;
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

    [Fact]
    public async Task Refresh_WithoutConnection_DoesNothing()
    {
        await SeedPublishedSetAsync(DeviationUrl(1), "DEV-A", DateTimeOffset.UtcNow.AddDays(-3));

        Assert.Null(await RefreshAsync());

        Assert.Empty(_factory.DeviantArt.MetadataRequests);
        Assert.Empty(await SnapshotsAsync());
    }
}
