using System.Net.Http.Json;
using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Generation;
using ArtStudio.Server.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ArtStudio.Server.Tests.Fakes.StudioAppFactory;

namespace ArtStudio.Server.Tests;

public sealed class VideoTimeEstimatorTests : IDisposable
{
    private const double PrototypeSeconds = 17 * 60 + 21;

    private readonly StudioAppFactory _factory = new();
    private readonly HttpClient _client;

    public VideoTimeEstimatorTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private static GenerationJob VideoJob(int setId, double seconds, double megapixels, bool turbo, JobStatus status = JobStatus.Completed) => new()
    {
        PromptSetId = setId,
        Kind = JobKind.Video,
        Prompt = "a fox",
        Resolution = VideoOptions.ResolutionLabel(megapixels),
        RequestedCount = 1,
        Status = status,
        VideoSeconds = seconds,
        VideoMegapixels = megapixels,
        VideoTurbo = turbo,
        VideoStartImagePath = "start.png",
    };

    /// <summary>Finished 5 s, 0.46 MP turbo videos that took the given render times.</summary>
    private async Task AddFinishedVideosAsync(params double?[] renderSeconds)
    {
        var setId = await _factory.CreateSetAsync(_client, "a fox", count: 1);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
        foreach (var seconds in renderSeconds)
        {
            var job = VideoJob(setId, 5, 0.46, turbo: true);
            db.Jobs.Add(job);
            await db.SaveChangesAsync();
            db.Videos.Add(new GeneratedVideo { PromptSetId = setId, GenerationJobId = job.Id, FilePath = "x.mp4", RenderSeconds = seconds });
        }
        await db.SaveChangesAsync();
    }

    private async Task<double> RateAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<VideoTimeEstimator>().SecondsPerUnitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WithoutHistory_ThePrototypeRunIsTheStartingPoint()
    {
        var estimate = VideoTimeEstimator.SecondsPerVideo(VideoJob(1, 5, 0.46, turbo: true), await RateAsync());

        Assert.Equal(PrototypeSeconds, estimate!.Value, precision: 6);
    }

    [Fact]
    public async Task Estimate_ScalesWithLengthSizeAndSteps()
    {
        await AddFinishedVideosAsync(600);
        var rate = await RateAsync();

        var same = VideoTimeEstimator.SecondsPerVideo(VideoJob(1, 5, 0.46, turbo: true), rate);
        var biggerSlower = VideoTimeEstimator.SecondsPerVideo(VideoJob(1, 10, 0.92, turbo: false), rate);

        Assert.Equal(600, same!.Value, precision: 6);
        // 243 instead of 124 frames, twice the pixels, 20 instead of 8 steps.
        Assert.Equal(600 * 243.0 / 124 * 2 * 20 / 8, biggerSlower!.Value, precision: 6);
    }

    [Fact]
    public async Task OneSlowVideo_DoesNotSkewTheEstimate()
    {
        await AddFinishedVideosAsync(600, 6000, 610, null);

        var estimate = VideoTimeEstimator.SecondsPerVideo(VideoJob(1, 5, 0.46, turbo: true), await RateAsync());

        Assert.Equal(610, estimate!.Value, precision: 6);
    }

    [Fact]
    public async Task ImageJobs_GetNoEstimate()
    {
        var job = new GenerationJob { Prompt = "a fox", Resolution = "Native", Kind = JobKind.Image };

        Assert.Null(VideoTimeEstimator.SecondsPerVideo(job, await RateAsync()));
    }

    [Fact]
    public async Task FinishedVideos_RecordTheirRenderTime_AndJobsCarryTheEstimate()
    {
        await _factory.ConnectGoogleAsync();
        var setId = await _factory.CreateSetAsync(_client, "a fox", count: 1);
        await WaitUntilAsync(async () => (await GetSetAsync(_client, setId)).Images.Count == 1, "image rendered");
        var imageId = (await GetSetAsync(_client, setId)).Images[0].Id;
        (await _client.PostAsJsonAsync($"/api/sets/{setId}/videos",
            new { startFrame = $"image:{imageId}", prompt = "a fox walks", seconds = 5, megapixels = 0.46, turbo = true, count = 1 }))
            .EnsureSuccessStatusCode();
        await WaitUntilAsync(async () => (await GetSetAsync(_client, setId)).Videos.Count == 1, "video downloaded");

        using var scope = _factory.Services.CreateScope();
        var video = await scope.ServiceProvider.GetRequiredService<StudioDbContext>().Videos.AsNoTracking().SingleAsync();
        Assert.NotNull(video.RenderSeconds);
        Assert.True(video.RenderSeconds >= 0);
        var jobs = (await GetSetAsync(_client, setId)).Jobs;
        Assert.NotNull(jobs.Single(j => j.Kind == JobKind.Video).EstimatedSecondsPerVideo);
        Assert.Null(jobs.Single(j => j.Kind == JobKind.Image).EstimatedSecondsPerVideo);
    }
}
