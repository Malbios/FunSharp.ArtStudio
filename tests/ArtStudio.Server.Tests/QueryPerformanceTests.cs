using System.Diagnostics;
using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace ArtStudio.Server.Tests;

/// <summary>
/// Guards the endpoints the app and the browser extension read often against queries that get slow as a studio grows:
/// a table scanned again for every row, one query per item, or simply a slow answer on a large studio.
/// </summary>
public sealed class QueryPerformanceTests(LargeStudio studio) : IClassFixture<LargeStudio>
{
    private const int AnswerBudgetMs = 500;

    /// <summary>{set}, {stash} and {preset} stand for a seeded set with everything, its Stash item, and a preset.</summary>
    public static TheoryData<string> OftenReadEndpoints =>
    [
        "/api/sets?stage=draft",
        "/api/sets?stage=working",
        "/api/sets?stage=ready",
        "/api/sets?stage=ready&preset={preset}",
        "/api/sets?stage=published",
        "/api/sets?stage=archived",
        "/api/sets/{set}",
        "/api/sets/{set}/video-frames",
        "/api/queue",
        "/api/prompt-queue",
        "/api/deviantart/known",
        "/api/stash/{stash}/extra-picks",
        "/api/stats",
        "/api/blocked-artists",
        "/api/building-blocks",
    ];

    [Theory]
    [MemberData(nameof(OftenReadEndpoints))]
    public async Task NoQueryScansATableAgainForEveryRow(string endpoint)
    {
        var commands = await SqlRecorder.RecordAsync(request => SendAsync(studio.Client, studio.Seeded.Url(endpoint), request));

        await using var connection = new SqliteConnection($"Data Source={studio.DatabasePath}");
        await connection.OpenAsync();
        var problems = new List<string>();
        foreach (var command in commands)
        {
            var plan = await QueryPlanAsync(connection, command);
            foreach (var scan in ScansInsideCorrelatedSubqueries(plan))
                problems.Add($"{scan}\n  in:\n{command.Sql}\n  plan:\n{string.Join('\n', plan.Select(row => $"    {row.Detail}"))}");
        }

        Assert.True(commands.Count > 0, $"{endpoint} ran no SQL; the recording missed it.");
        Assert.True(problems.Count == 0, $"{endpoint} runs a subquery that scans a whole table once per row:\n{string.Join("\n\n", problems)}");
    }

    [Theory]
    [MemberData(nameof(OftenReadEndpoints))]
    public async Task RunsAsManyQueriesForTenTimesTheSets(string endpoint)
    {
        using var factory = new StudioAppFactory();
        using var client = factory.CreateClient();
        var seeded = await StudioSeed.AddAsync(factory, 0, 12);
        var small = await SqlRecorder.RecordAsync(request => SendAsync(client, seeded.Url(endpoint), request));
        await StudioSeed.AddAsync(factory, 12, 108);
        var large = await SqlRecorder.RecordAsync(request => SendAsync(client, seeded.Url(endpoint), request));

        Assert.True(small.Count == large.Count,
            $"{endpoint} ran {small.Count} queries for 12 sets but {large.Count} for 120, so it queries per item.");
    }

    [Theory]
    [MemberData(nameof(OftenReadEndpoints))]
    public async Task AnswersQuicklyForALargeStudio(string endpoint)
    {
        var url = studio.Seeded.Url(endpoint);
        await SendAsync(studio.Client, url, new HttpRequestMessage());
        var fastestMs = long.MaxValue;
        for (var run = 0; run < 3; run++)
        {
            var watch = Stopwatch.StartNew();
            await SendAsync(studio.Client, url, new HttpRequestMessage());
            fastestMs = Math.Min(fastestMs, watch.ElapsedMilliseconds);
        }

        Assert.True(fastestMs <= AnswerBudgetMs,
            $"{endpoint} took {fastestMs} ms at best for {LargeStudio.SetCount} sets; the budget is {AnswerBudgetMs} ms.");
    }

    private static async Task SendAsync(HttpClient client, string url, HttpRequestMessage request)
    {
        request.Method = HttpMethod.Get;
        request.RequestUri = new Uri(url, UriKind.Relative);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        await response.Content.ReadAsByteArrayAsync();
    }

    private sealed record PlanRow(int Id, int Parent, string Detail);

    private static async Task<IReadOnlyList<PlanRow>> QueryPlanAsync(SqliteConnection connection, RecordedCommand recorded)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + recorded.Sql;
        foreach (var (name, value) in recorded.Parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        var rows = new List<PlanRow>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add(new PlanRow(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(3)));
        return rows;
    }

    /// <summary>
    /// A correlated subquery runs once per row of the outer query, so it may only SEARCH (look up by index); a SCAN
    /// in it reads a whole table per row.
    /// </summary>
    private static IEnumerable<string> ScansInsideCorrelatedSubqueries(IReadOnlyList<PlanRow> plan)
    {
        var correlated = plan.Where(row => row.Detail.StartsWith("CORRELATED", StringComparison.Ordinal)).Select(row => row.Id).ToHashSet();
        var byId = plan.ToDictionary(row => row.Id);
        bool InsideCorrelated(PlanRow row)
        {
            for (var parent = row.Parent; byId.TryGetValue(parent, out var above); parent = above.Parent)
                if (correlated.Contains(parent))
                    return true;
            return correlated.Contains(row.Parent);
        }
        return plan.Where(row => row.Detail.StartsWith("SCAN ", StringComparison.Ordinal) && InsideCorrelated(row)).Select(row => row.Detail);
    }
}

public sealed class LargeStudio : IAsyncLifetime
{
    public const int SetCount = 3000;

    public StudioAppFactory Factory { get; } = new();

    public HttpClient Client { get; private set; } = null!;

    public SeededIds Seeded { get; private set; } = null!;

    public string DatabasePath => Path.Combine(Factory.DataDirectory, "artstudio.db");

    public async Task InitializeAsync()
    {
        Client = Factory.CreateClient();
        Seeded = await StudioSeed.AddAsync(Factory, 0, SetCount);
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        Factory.Dispose();
        return Task.CompletedTask;
    }
}

public sealed record SeededIds(int SetId, long StashItemId, int PresetId)
{
    public string Url(string endpoint) => endpoint
        .Replace("{set}", SetId.ToString())
        .Replace("{stash}", StashItemId.ToString())
        .Replace("{preset}", PresetId.ToString());
}

/// <summary>
/// Sets in every stage with what the listings read: jobs, images, picks, videos, stats and many artists. Nothing is
/// left for the background workers to do, so they stay out of the way.
/// </summary>
public static class StudioSeed
{
    private const string ArtStyle = "soft watercolor";
    private static readonly string LongText = string.Concat(Enumerable.Repeat("A misty forest at dawn, layered hills. ", 100));

    public static async Task<SeededIds> AddAsync(StudioAppFactory factory, int firstIndex, int count)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
        var now = DateTimeOffset.UtcNow;

        var sets = Enumerable.Range(firstIndex, count).Select(index => NewSet(index, now)).ToList();
        db.PromptSets.AddRange(sets);
        if (firstIndex == 0)
        {
            db.BuildingBlocks.Add(new BuildingBlock { Kind = BuildingBlockKind.Preset, Label = "Watercolor", Text = "", ArtStyle = ArtStyle });
            db.BuildingBlocks.Add(new BuildingBlock { Kind = BuildingBlockKind.Text, Label = "Signature", Text = "made in Art Studio" });
            db.BlockedArtists.Add(new BlockedArtist { Username = "blocked-artist", CreatedAt = now });
        }
        await db.SaveChangesAsync();

        var images = sets.SelectMany(set => set.Jobs.Where(job => job.Kind == JobKind.Image && job.Status == JobStatus.Completed)
                .SelectMany(job => Enumerable.Range(0, 2).Select(n =>
                    new GeneratedImage { PromptSetId = set.Id, GenerationJobId = job.Id, FilePath = $"{set.Id}-{n}.png", CreatedAt = now })))
            .ToList();
        db.Images.AddRange(images);
        db.Videos.AddRange(sets.SelectMany(set => set.Jobs.Where(job => job.Kind == JobKind.Video)
            .Select(job => new GeneratedVideo { PromptSetId = set.Id, GenerationJobId = job.Id, FilePath = $"{set.Id}.mp4", CreatedAt = now })));
        db.DeviationStats.AddRange(sets.Where(set => set.PublishedAt is not null).SelectMany(set => Enumerable.Range(1, 2).Select(day =>
            new DeviationStatsSnapshot { PromptSetId = set.Id, Day = DateOnly.FromDateTime(DateTime.Now).AddDays(-day), FetchedAt = now, Views = day * 10 })));
        await db.SaveChangesAsync();

        var drafts = sets.Where(set => set.IsDraft).Select(set => set.Id).ToHashSet();
        db.Picks.AddRange(images.Where(image => !drafts.Contains(image.PromptSetId))
            .GroupBy(image => image.PromptSetId)
            .SelectMany(group => group.Select((image, position) =>
                new PickedImage { PromptSetId = group.Key, GeneratedImageId = image.Id, Position = position + 1 })));
        await db.SaveChangesAsync();

        var full = sets.First(set => set.StashItemId is not null);
        var preset = db.BuildingBlocks.Single(block => block.Kind == BuildingBlockKind.Preset);
        return new SeededIds(full.Id, full.StashItemId!.Value, preset.Id);
    }

    private static PromptSet NewSet(int index, DateTimeOffset now)
    {
        var prompt = index % 2 == 0 ? $"{LongText} {ArtStyle}" : LongText;
        var set = new PromptSet
        {
            Prompt = prompt,
            Resolution = "Wide",
            SourceKind = SourceKind.DeviantArt,
            DeviantArtUrl = $"https://www.deviantart.com/artist{index / 10}/art/Forest-{index}",
            DeviationId = $"{index}",
            DeviantArtAuthor = $"artist{index / 10}",
            CreatedAt = now.AddMinutes(-index),
            PromptGeneration = PromptGenerationState.Done,
            GeneratedPrompt = LongText,
            VisionContext = "the fog",
            Jobs =
            [
                new GenerationJob { Prompt = prompt, Resolution = "Wide", RequestedCount = 2, CompletedCount = 2, Status = JobStatus.Completed, CreatedAt = now },
            ],
        };
        if (index % 5 == 0)
            set.Jobs.Add(new GenerationJob { Prompt = prompt, Resolution = "Wide", RequestedCount = 1, Status = JobStatus.Failed, Error = "ComfyUI failed", CreatedAt = now });
        if (index % 3 == 0)
            set.Jobs.Add(new GenerationJob { Kind = JobKind.Video, Prompt = prompt, Resolution = "Wide", RequestedCount = 1, CompletedCount = 1, Status = JobStatus.Completed, CreatedAt = now, VideoSeconds = 5 });

        switch (index % 6)
        {
            case 0:
                set.IsDraft = true;
                break;
            case 2 or 3:
                set.ReadyToPostAt = now.AddMinutes(-index);
                set.PostTitle = "Misty forest";
                set.PostTags = "forest fog";
                if (index % 6 == 3)
                {
                    set.StashItemId = 1000 + index;
                    set.StashedAt = now;
                }
                break;
            case 4:
                set.ReadyToPostAt = now.AddDays(-2);
                set.PostTitle = "Misty forest";
                set.PostTags = "forest fog";
                set.PublishedAt = now.AddDays(-1).AddMinutes(-index);
                set.PublishedUrl = $"https://www.deviantart.com/studio/art/Misty-{index}";
                set.PublishedDeviationId = $"9{index}";
                break;
            case 5:
                set.ArchivedAt = now.AddMinutes(-index);
                break;
        }
        return set;
    }
}
