using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArtStudio.Server.Api;
using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Stats;
using ArtStudio.Server.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ArtStudio.Server.Tests.Fakes.StudioAppFactory;

namespace ArtStudio.Server.Tests;

public sealed class VideoTests : IDisposable
{
    private const string VideoPrompt = "The fox turns its head and winks.";

    private readonly StudioAppFactory _factory = new();
    private readonly HttpClient _client;

    public VideoTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private async Task<SetDetailDto> SetWithImagesAsync(int images = 2)
    {
        var setId = await _factory.CreateSetAsync(_client, "a fox", count: images);
        await WaitUntilAsync(async () => (await GetSetAsync(_client, setId)).Images.Count == images, "images rendered");
        return await GetSetAsync(_client, setId);
    }

    private Task<HttpResponseMessage> MakeVideoAsync(
        int setId, int startImageId, int? endImageId = null, double seconds = 5, double megapixels = 0.46,
        bool turbo = true, int count = 1, bool queueFirst = false) =>
        MakeVideoFromFramesAsync(setId, $"image:{startImageId}", endImageId is int end ? $"image:{end}" : null,
            seconds, megapixels, turbo, count, queueFirst);

    private Task<HttpResponseMessage> MakeVideoFromFramesAsync(
        int setId, string startFrame, string? endFrame, double seconds = 5, double megapixels = 0.46,
        bool turbo = true, int count = 1, bool queueFirst = false) =>
        _client.PostAsJsonAsync($"/api/sets/{setId}/videos",
            new { startFrame, endFrame, prompt = VideoPrompt, seconds, megapixels, turbo, count, queueFirst });

    private async Task<List<VideoFrameDto>> FramesAsync(int setId) =>
        (await _client.GetFromJsonAsync<List<VideoFrameDto>>($"/api/sets/{setId}/video-frames", Json))!;

    private Task WaitForVideoJobAsync(int setId, JobStatus status) =>
        WaitUntilAsync(async () => (await GetSetAsync(_client, setId)).Jobs.Any(j => j.Kind == JobKind.Video && j.Status == status),
            $"video job {status}");

    private static JsonNode Inputs(JsonObject workflow, string nodeId) => workflow[nodeId]!["inputs"]!;

    private async Task<(bool Images, bool Videos)> PausedAsync()
    {
        var queue = await GetQueueAsync(_client);
        return (queue.ImagesPaused, queue.VideosPaused);
    }

    private async Task<JobStatus> SingleJobStatusAsync(int setId) => (await GetSetAsync(_client, setId)).Jobs.Single().Status;

    private async Task<GenerationJob> VideoJobAsync(StudioAppFactory app)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<StudioDbContext>().Jobs.AsNoTracking()
            .SingleAsync(j => j.Kind == JobKind.Video);
    }

    [Fact]
    public async Task MakeVideo_RendersOnComfyAndDownloadsFromDrive()
    {
        await _factory.ConnectGoogleAsync();
        var set = await SetWithImagesAsync();

        (await MakeVideoAsync(set.Id, set.Images[0].Id, set.Images[1].Id, seconds: 6, megapixels: 0.5, turbo: false, count: 2))
            .EnsureSuccessStatusCode();
        await WaitForVideoJobAsync(set.Id, JobStatus.Completed);

        var workflows = _factory.Comfy.SubmittedVideoWorkflows;
        Assert.Equal(2, workflows.Count);
        Assert.Equal(4, _factory.Comfy.UploadedFileNames.Count);
        foreach (var workflow in workflows)
        {
            Assert.Contains(Inputs(workflow, "114")["image"]!.GetValue<string>(), _factory.Comfy.UploadedFileNames);
            Assert.Contains(Inputs(workflow, "128")["image"]!.GetValue<string>(), _factory.Comfy.UploadedFileNames);
            Assert.Equal(VideoPrompt, Inputs(workflow, "135:105:104")["prompt"]!.GetValue<string>());
            Assert.Equal(6, Inputs(workflow, "135:279")["value"]!.GetValue<double>());
            Assert.Equal(0.5, Inputs(workflow, "135:290")["value"]!.GetValue<double>());
            Assert.False(Inputs(workflow, "135:105:218:126")["value"]!.GetValue<bool>());
            Assert.StartsWith("video/artstudio-", Inputs(workflow, "92")["filename_prefix"]!.GetValue<string>());
        }
        Assert.NotEqual(Inputs(workflows[0], "92")["filename_prefix"]!.ToString(), Inputs(workflows[1], "92")["filename_prefix"]!.ToString());
        var videoSeeds = _factory.Comfy.SubmittedSeeds.Skip(set.Images.Count).ToList();
        Assert.Equal(2, videoSeeds.Distinct().Count());
        Assert.DoesNotContain(_factory.Comfy.ViewedFileNames, name => name.EndsWith(".mp4"));

        var updated = await GetSetAsync(_client, set.Id);
        Assert.Equal(2, updated.Videos.Count);
        Assert.Equal(2, updated.Images.Count);
        var job = updated.Jobs.Single(j => j.Kind == JobKind.Video);
        Assert.Equal(("960 x 544", 6.0, false, true), (job.Resolution, job.VideoSeconds!.Value, job.VideoTurbo, job.HasEndFrame));
        var video = await _client.GetAsync(updated.Videos[0].Url);
        Assert.Equal("video/mp4", video.Content.Headers.ContentType?.MediaType);
        Assert.Equal(VideoBytes, await video.Content.ReadAsByteArrayAsync());
        Assert.Equal(await (await _client.GetAsync(set.Images[0].Url)).Content.ReadAsByteArrayAsync(),
            await _client.GetByteArrayAsync(updated.Videos[0].StartImageUrl));
        Assert.Equal(videoSeeds[0].ToString(), updated.Videos[0].Seed);
    }

    [Fact]
    public async Task MakeVideo_WithoutEndFrame_LeavesTheEndFrameOut()
    {
        await _factory.ConnectGoogleAsync();
        var set = await SetWithImagesAsync(images: 1);

        (await MakeVideoAsync(set.Id, set.Images[0].Id)).EnsureSuccessStatusCode();
        await WaitForVideoJobAsync(set.Id, JobStatus.Completed);

        var workflow = Assert.Single(_factory.Comfy.SubmittedVideoWorkflows);
        Assert.False(workflow.ContainsKey("128"));
        Assert.Null(Inputs(workflow, "135:105:104")["last_frame"]);
        Assert.Single(_factory.Comfy.UploadedFileNames);
        Assert.Null((await GetSetAsync(_client, set.Id)).Videos.Single().EndImageUrl);
    }

    [Fact]
    public async Task MakeVideo_WithoutGoogleDrive_IsRefused()
    {
        var set = await SetWithImagesAsync(images: 1);

        var response = await MakeVideoAsync(set.Id, set.Images[0].Id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Connect Google Drive", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
        Assert.DoesNotContain((await GetSetAsync(_client, set.Id)).Jobs, j => j.Kind == JobKind.Video);
    }

    [Theory]
    [InlineData(0, 0.46, 1)]
    [InlineData(31, 0.46, 1)]
    [InlineData(5, 0.99, 1)]
    [InlineData(5, 0.46, 21)]
    public async Task MakeVideo_OutOfRangeValues_AreRefused(double seconds, double megapixels, int count)
    {
        await _factory.ConnectGoogleAsync();
        var set = await SetWithImagesAsync(images: 1);

        var response = await MakeVideoAsync(set.Id, set.Images[0].Id, seconds: seconds, megapixels: megapixels, count: count);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MakeVideo_WithAnotherSetsImage_IsRefused()
    {
        await _factory.ConnectGoogleAsync();
        var set = await SetWithImagesAsync(images: 1);
        var other = await SetWithImagesAsync(images: 1);

        var response = await MakeVideoAsync(set.Id, set.Images[0].Id, other.Images[0].Id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DriveFailure_FailsTheJob_AndRetryDownloadsWithoutRenderingAgain()
    {
        await _factory.ConnectGoogleAsync();
        var set = await SetWithImagesAsync(images: 1);
        _factory.Google.DriveFailure = HttpStatusCode.ServiceUnavailable;

        (await MakeVideoAsync(set.Id, set.Images[0].Id)).EnsureSuccessStatusCode();
        await WaitForVideoJobAsync(set.Id, JobStatus.Failed);
        Assert.Equal((false, true), await PausedAsync());

        _factory.Google.DriveFailure = null;
        var jobId = (await GetSetAsync(_client, set.Id)).Jobs.Single(j => j.Kind == JobKind.Video).Id;
        (await _client.PostAsync($"/api/jobs/{jobId}/retry", null)).EnsureSuccessStatusCode();
        await WaitForVideoJobAsync(set.Id, JobStatus.Completed);

        Assert.Single(_factory.Comfy.SubmittedVideoWorkflows);
        Assert.Single((await GetSetAsync(_client, set.Id)).Videos);
    }

    [Fact]
    public async Task VideoThatNeverReachesDrive_FailsWithAHint()
    {
        using var factory = new StudioAppFactory { DriveTimeout = TimeSpan.FromMilliseconds(200) };
        factory.VideosReachDrive = false;
        var client = factory.CreateClient();
        await factory.ConnectGoogleAsync();
        var setId = await factory.CreateSetAsync(client, "a fox", count: 1);
        await WaitUntilAsync(async () => (await GetSetAsync(client, setId)).Images.Count == 1, "image rendered");
        var imageId = (await GetSetAsync(client, setId)).Images[0].Id;

        (await client.PostAsJsonAsync($"/api/sets/{setId}/videos",
            new { startFrame = $"image:{imageId}", prompt = VideoPrompt, seconds = 5, megapixels = 0.46, turbo = true, count = 1 }))
            .EnsureSuccessStatusCode();
        await WaitUntilAsync(async () => (await GetSetAsync(client, setId)).Jobs.Any(j => j.Status == JobStatus.Failed), "job failed");

        var job = (await GetSetAsync(client, setId)).Jobs.Single(j => j.Kind == JobKind.Video);
        Assert.Contains("did not appear in the Google Drive folder 'ComfyUI/output/video'", job.Error);
    }

    [Fact]
    public async Task Restart_WhileWaitingForDrive_DownloadsWithoutRenderingAgain()
    {
        var comfy = new FakeComfyServer();
        var dataDirectory = Path.Combine(Path.GetTempPath(), "ArtStudioTests", Guid.NewGuid().ToString("N"));

        int setId;
        string fileName;
        using (var before = new StudioAppFactory
               { DataDirectory = dataDirectory, Comfy = comfy, KeepDataDirectory = true, DriveTimeout = TimeSpan.FromMinutes(1) })
        {
            before.VideosReachDrive = false;
            var client = before.CreateClient();
            await before.ConnectGoogleAsync();
            setId = await before.CreateSetAsync(client, "a fox", count: 1);
            await WaitUntilAsync(async () => (await GetSetAsync(client, setId)).Images.Count == 1, "image rendered");
            var imageId = (await GetSetAsync(client, setId)).Images[0].Id;
            (await client.PostAsJsonAsync($"/api/sets/{setId}/videos",
                new { startFrame = $"image:{imageId}", prompt = VideoPrompt, seconds = 5, megapixels = 0.46, turbo = true, count = 1 }))
                .EnsureSuccessStatusCode();
            await WaitUntilAsync(async () => (await VideoJobAsync(before)).CurrentOutputFileName is not null, "video rendered");
            fileName = Path.GetFileName((await VideoJobAsync(before)).CurrentOutputFileName!);
        }

        using var after = new StudioAppFactory { DataDirectory = dataDirectory, Comfy = comfy };
        after.Google.AddFile("ComfyUI/output/video", fileName, VideoBytes);
        var restarted = after.CreateClient();
        await WaitUntilAsync(async () => (await GetSetAsync(restarted, setId)).Videos.Count == 1, "video downloaded");

        Assert.Single(comfy.SubmittedVideoWorkflows);
        var job = await VideoJobAsync(after);
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Null(job.CurrentOutputFileName);
    }

    [Fact]
    public async Task Queue_RunsWaitingImagesBeforeSwitchingToVideos()
    {
        await _factory.ConnectGoogleAsync();
        var set = await SetWithImagesAsync(images: 1);
        _factory.Comfy.HoldRuns = true;
        var running = await _factory.CreateSetAsync(_client, "running image", count: 1);
        await WaitUntilAsync(async () => (await GetSetAsync(_client, running)).Jobs.Single().Status == JobStatus.Running, "image running");

        (await MakeVideoAsync(set.Id, set.Images[0].Id)).EnsureSuccessStatusCode();
        var laterImage = await _factory.CreateSetAsync(_client, "later image", count: 1);

        var queued = (await GetQueueAsync(_client)).Active;
        Assert.Equal([running, laterImage, set.Id], queued.Select(j => j.SetId));
        Assert.Equal([JobKind.Image, JobKind.Image, JobKind.Video], queued.Select(j => j.Kind));

        _factory.Comfy.HoldRuns = false;
        await WaitForVideoJobAsync(set.Id, JobStatus.Completed);
        Assert.Equal(["a fox", "running image", "later image", VideoPrompt], _factory.Comfy.SubmittedPrompts);
        Assert.True((await GetQueueAsync(_client)).SharedServer);
    }

    private const string VideoServerHost = "video-vm:8188";

    private async Task UseVideoServerAsync()
    {
        var current = await _client.GetFromJsonAsync<SettingsDto>("/api/settings", Json);
        (await _client.PutAsJsonAsync("/api/settings", current! with { VideoComfyServerUrl = $"http://{VideoServerHost}" }))
            .EnsureSuccessStatusCode();
    }

    private async Task<(int ImageSetId, int VideoSetId)> RunImageAndVideoTogetherAsync()
    {
        await _factory.ConnectGoogleAsync();
        var set = await SetWithImagesAsync(images: 1);
        await UseVideoServerAsync();
        _factory.Comfy.HoldRuns = true;

        var image = await _factory.CreateSetAsync(_client, "running image", count: 1);
        (await MakeVideoAsync(set.Id, set.Images[0].Id)).EnsureSuccessStatusCode();
        await WaitUntilAsync(
            async () => (await GetQueueAsync(_client)).Active.Count(j => j.Status == JobStatus.Running) == 2,
            "image and video running at the same time");
        return (image, set.Id);
    }

    [Fact]
    public async Task SeparateVideoServer_RendersVideosWhileImagesRender()
    {
        var (image, videoSet) = await RunImageAndVideoTogetherAsync();
        Assert.False((await GetQueueAsync(_client)).SharedServer);

        _factory.Comfy.HoldRuns = false;
        await WaitForVideoJobAsync(videoSet, JobStatus.Completed);
        await WaitUntilAsync(async () => await SingleJobStatusAsync(image) == JobStatus.Completed, "image done");

        var imageHost = new Uri(SettingsStore.DefaultComfyServerUrl).Authority;
        var hostByPrompt = _factory.Comfy.SubmittedPrompts.Zip(_factory.Comfy.SubmittedHosts).ToDictionary();
        Assert.Equal(VideoServerHost, hostByPrompt[VideoPrompt]);
        Assert.Equal(imageHost, hostByPrompt["running image"]);
        Assert.Equal(imageHost, hostByPrompt["a fox"]);
    }

    [Fact]
    public async Task SeparateVideoServer_CancellingTheVideo_LeavesTheImageRunning()
    {
        var (image, videoSet) = await RunImageAndVideoTogetherAsync();
        var videoJob = (await GetSetAsync(_client, videoSet)).Jobs.Single(j => j.Kind == JobKind.Video);

        (await _client.PostAsync($"/api/jobs/{videoJob.Id}/cancel", null)).EnsureSuccessStatusCode();
        await WaitForVideoJobAsync(videoSet, JobStatus.Cancelled);
        Assert.Equal(JobStatus.Running, await SingleJobStatusAsync(image));

        _factory.Comfy.HoldRuns = false;
        await WaitUntilAsync(async () => await SingleJobStatusAsync(image) == JobStatus.Completed, "image done");
    }

    [Fact]
    public async Task PausedImages_LetVideosRunWithoutSwitchingModels()
    {
        await _factory.ConnectGoogleAsync();
        var set = await SetWithImagesAsync(images: 1);
        (await _client.PostAsync("/api/queue/pause?kind=Image", null)).EnsureSuccessStatusCode();

        var waitingImage = await _factory.CreateSetAsync(_client, "waiting image", count: 1);
        (await MakeVideoAsync(set.Id, set.Images[0].Id)).EnsureSuccessStatusCode();
        await WaitForVideoJobAsync(set.Id, JobStatus.Completed);

        Assert.Equal(JobStatus.Queued, await SingleJobStatusAsync(waitingImage));
        Assert.Equal((true, false), await PausedAsync());
        Assert.DoesNotContain("waiting image", _factory.Comfy.SubmittedPrompts);

        (await _client.PostAsync("/api/queue/resume?kind=Image", null)).EnsureSuccessStatusCode();
        await WaitUntilAsync(async () => await SingleJobStatusAsync(waitingImage) == JobStatus.Completed, "image ran after resuming");
    }

    [Fact]
    public async Task PausedVideos_LetImagesRun()
    {
        await _factory.ConnectGoogleAsync();
        var set = await SetWithImagesAsync(images: 1);
        (await _client.PostAsync("/api/queue/pause?kind=Video", null)).EnsureSuccessStatusCode();

        (await MakeVideoAsync(set.Id, set.Images[0].Id)).EnsureSuccessStatusCode();
        var image = await _factory.CreateSetAsync(_client, "image", count: 1);
        await WaitUntilAsync(async () => await SingleJobStatusAsync(image) == JobStatus.Completed, "image ran");

        Assert.Equal(JobStatus.Queued, (await GetSetAsync(_client, set.Id)).Jobs.Single(j => j.Kind == JobKind.Video).Status);
        Assert.Empty(_factory.Comfy.SubmittedVideoWorkflows);
        Assert.Equal((false, true), await PausedAsync());
    }

    [Fact]
    public async Task PauseAndResumeWithoutKind_ActOnBoth()
    {
        (await _client.PostAsync("/api/queue/pause", null)).EnsureSuccessStatusCode();
        Assert.Equal((true, true), await PausedAsync());

        (await _client.PostAsync("/api/queue/resume?kind=Video", null)).EnsureSuccessStatusCode();
        Assert.Equal((true, false), await PausedAsync());

        (await _client.PostAsync("/api/queue/resume", null)).EnsureSuccessStatusCode();
        Assert.Equal((false, false), await PausedAsync());
    }

    [Fact]
    public async Task QueueFirst_AndMoves_StayWithinTheirKind()
    {
        await _factory.ConnectGoogleAsync();
        var set = await SetWithImagesAsync(images: 1);
        (await _client.PostAsync("/api/queue/pause", null)).EnsureSuccessStatusCode();

        (await MakeVideoAsync(set.Id, set.Images[0].Id, seconds: 3)).EnsureSuccessStatusCode();
        (await MakeVideoAsync(set.Id, set.Images[0].Id, seconds: 8, queueFirst: true)).EnsureSuccessStatusCode();
        var image = await _factory.CreateSetAsync(_client, "image", count: 1);

        async Task<List<string>> OrderAsync() =>
            (await GetQueueAsync(_client)).Active.Select(j => j.Kind == JobKind.Video ? $"video {j.VideoSeconds}s" : "image").ToList();
        Assert.Equal(["image", "video 8s", "video 3s"], await OrderAsync());

        var threeSeconds = (await GetQueueAsync(_client)).Active.Single(j => j.VideoSeconds == 3).Id;
        (await _client.PostAsJsonAsync($"/api/jobs/{threeSeconds}/move", new { move = "Top" })).EnsureSuccessStatusCode();
        Assert.Equal(["image", "video 3s", "video 8s"], await OrderAsync());
        var imageJob = (await GetQueueAsync(_client)).Active.Single(j => j.SetId == image).Id;
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _client.PostAsJsonAsync($"/api/jobs/{imageJob}/move", new { move = "Down" })).StatusCode);
    }

    private static MultipartFormDataContent VideoForm(bool withEndImage, string sourceKind = "Upload")
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(VideoPrompt), "prompt" },
            { new StringContent("Wide"), "resolution" },
            { new StringContent("1"), "count" },
            { new StringContent("video"), "mode" },
            { new StringContent("5"), "seconds" },
            { new StringContent("0.46"), "megapixels" },
            { new StringContent("true"), "turbo" },
            { new StringContent(sourceKind), "sourceKind" },
        };
        var start = new ByteArrayContent([1, 2, 3]);
        start.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(start, "image", "start.png");
        if (withEndImage)
        {
            var end = new ByteArrayContent([4, 5, 6]);
            end.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(end, "endImage", "end.jpg");
        }
        return form;
    }

    [Fact]
    public async Task NewVideoSet_StartsFromTheUploadedImages()
    {
        await _factory.ConnectGoogleAsync();

        var response = await _client.PostAsync("/api/sets", VideoForm(withEndImage: true));
        response.EnsureSuccessStatusCode();
        var setId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        await WaitForVideoJobAsync(setId, JobStatus.Completed);

        var set = await GetSetAsync(_client, setId);
        Assert.Equal(SourceKind.Upload, set.SourceKind);
        Assert.Empty(set.Images);
        var video = Assert.Single(set.Videos);
        Assert.Equal([1, 2, 3], await _client.GetByteArrayAsync(video.StartImageUrl));
        Assert.Equal([4, 5, 6], await _client.GetByteArrayAsync(video.EndImageUrl));
        Assert.True(Assert.Single(_factory.Comfy.SubmittedVideoWorkflows).ContainsKey("128"));
    }

    [Fact]
    public async Task NewVideoSet_OffersItsUploadedFrames_ForMakingItAgain()
    {
        await _factory.ConnectGoogleAsync();
        var response = await _client.PostAsync("/api/sets", VideoForm(withEndImage: true));
        var setId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        await WaitForVideoJobAsync(setId, JobStatus.Completed);
        var firstJob = (await GetSetAsync(_client, setId)).Jobs.Single();

        var frames = await FramesAsync(setId);
        var video = (await GetSetAsync(_client, setId)).Videos.Single();

        Assert.Equal(["source", $"job:{firstJob.Id}:end"], frames.Select(f => f.Key));
        Assert.Equal("Uploaded image", frames[0].Label);
        Assert.Equal(("source", $"job:{firstJob.Id}:end"), (video.StartFrameKey, video.EndFrameKey));
        Assert.Equal(0.46, video.Megapixels);

        (await MakeVideoFromFramesAsync(setId, "source", $"job:{firstJob.Id}:end")).EnsureSuccessStatusCode();
        await WaitUntilAsync(async () => (await GetSetAsync(_client, setId)).Videos.Count == 2, "second video");
        var again = (await GetSetAsync(_client, setId)).Videos[1];
        Assert.Equal(("source", $"job:{firstJob.Id}:end"), (again.StartFrameKey, again.EndFrameKey));
        Assert.Equal([1, 2, 3], await _client.GetByteArrayAsync(again.StartImageUrl));
        Assert.Equal([4, 5, 6], await _client.GetByteArrayAsync(again.EndImageUrl));
    }

    [Fact]
    public async Task FramesOfGeneratedImages_AreKeyedByImage()
    {
        await _factory.ConnectGoogleAsync();
        var set = await SetWithImagesAsync();

        (await MakeVideoAsync(set.Id, set.Images[1].Id, set.Images[0].Id)).EnsureSuccessStatusCode();
        await WaitForVideoJobAsync(set.Id, JobStatus.Completed);

        var video = (await GetSetAsync(_client, set.Id)).Videos.Single();
        Assert.Equal(($"image:{set.Images[1].Id}", $"image:{set.Images[0].Id}"), (video.StartFrameKey, video.EndFrameKey));
        Assert.Equal([$"image:{set.Images[0].Id}", $"image:{set.Images[1].Id}"], (await FramesAsync(set.Id)).Select(f => f.Key));
    }

    private async Task<SetDetailDto> SetWithVideoAsync()
    {
        await _factory.ConnectGoogleAsync();
        var set = await SetWithImagesAsync(images: 1);
        (await MakeVideoAsync(set.Id, set.Images[0].Id)).EnsureSuccessStatusCode();
        await WaitUntilAsync(async () => (await GetSetAsync(_client, set.Id)).Videos.Count == 1, "video downloaded");
        return await GetSetAsync(_client, set.Id);
    }

    private Task<HttpResponseMessage> PickVideoAsync(int videoId, bool picked) =>
        _client.PutAsJsonAsync($"/api/videos/{videoId}/pick", new { picked });

    private async Task<bool> IsPickedAsync(int setId) => (await GetSetAsync(_client, setId)).Videos.Single().Picked;

    [Fact]
    public async Task Videos_CanBePickedAndUnpicked()
    {
        var set = await SetWithVideoAsync();
        Assert.False(set.Videos.Single().Picked);

        (await PickVideoAsync(set.Videos.Single().Id, true)).EnsureSuccessStatusCode();
        Assert.True(await IsPickedAsync(set.Id));

        (await PickVideoAsync(set.Videos.Single().Id, false)).EnsureSuccessStatusCode();
        Assert.False(await IsPickedAsync(set.Id));
    }

    [Fact]
    public async Task VideosOfPublishedAndArchivedSets_CanStillBePicked()
    {
        var set = await SetWithVideoAsync();
        var videoId = set.Videos.Single().Id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
            var stored = await db.PromptSets.SingleAsync(s => s.Id == set.Id);
            stored.ReadyToPostAt = stored.PublishedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        (await PickVideoAsync(videoId, true)).EnsureSuccessStatusCode();
        Assert.True(await IsPickedAsync(set.Id));

        (await _client.PostAsync($"/api/sets/{set.Id}/archive", null)).EnsureSuccessStatusCode();
        (await PickVideoAsync(videoId, false)).EnsureSuccessStatusCode();
        Assert.False(await IsPickedAsync(set.Id));
    }

    [Fact]
    public async Task SetList_CountsEachSetsVideos()
    {
        var withVideo = await SetWithVideoAsync();
        var withoutVideo = await SetWithImagesAsync(images: 1);

        var listed = (await _client.GetFromJsonAsync<List<SetSummaryDto>>("/api/sets?stage=working", Json))!;

        Assert.Equal(1, listed.Single(s => s.Id == withVideo.Id).VideoCount);
        Assert.Equal(0, listed.Single(s => s.Id == withoutVideo.Id).VideoCount);
    }

    [Fact]
    public async Task PickingAnUnknownVideo_IsNotFound()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await PickVideoAsync(999, true)).StatusCode);
    }

    private Task<HttpResponseMessage> MarkViewedAsync(int videoId) => _client.PostAsync($"/api/videos/{videoId}/viewed", null);

    private async Task<int> UnviewedVideoCountAsync(int setId) =>
        (await _client.GetFromJsonAsync<List<SetSummaryDto>>("/api/sets?stage=working", Json))!.Single(s => s.Id == setId).UnviewedVideoCount;

    private async Task<DateTimeOffset?> ViewedAtAsync(int videoId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
        return (await db.Videos.SingleAsync(v => v.Id == videoId)).ViewedAt;
    }

    [Fact]
    public async Task NewVideo_IsUnviewed_UntilMarkedViewed()
    {
        var set = await SetWithVideoAsync();
        var videoId = set.Videos.Single().Id;
        Assert.False(set.Videos.Single().Viewed);
        Assert.Equal(1, await UnviewedVideoCountAsync(set.Id));

        (await MarkViewedAsync(videoId)).EnsureSuccessStatusCode();

        Assert.True((await GetSetAsync(_client, set.Id)).Videos.Single().Viewed);
        Assert.Equal(0, await UnviewedVideoCountAsync(set.Id));
    }

    [Fact]
    public async Task MarkingAVideoViewedAgain_KeepsWhenItWasFirstViewed()
    {
        var set = await SetWithVideoAsync();
        var videoId = set.Videos.Single().Id;
        (await MarkViewedAsync(videoId)).EnsureSuccessStatusCode();
        var firstViewedAt = await ViewedAtAsync(videoId);

        (await MarkViewedAsync(videoId)).EnsureSuccessStatusCode();

        Assert.NotNull(firstViewedAt);
        Assert.Equal(firstViewedAt, await ViewedAtAsync(videoId));
    }

    [Fact]
    public async Task MarkingAnUnknownVideoViewed_IsNotFound()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await MarkViewedAsync(999)).StatusCode);
    }

    [Fact]
    public async Task DeletedVideo_LeavesTheSet_ButKeepsItsFile()
    {
        var set = await SetWithVideoAsync();
        var video = set.Videos.Single();
        string filePath;
        using (var scope = _factory.Services.CreateScope())
            filePath = (await scope.ServiceProvider.GetRequiredService<StudioDbContext>().Videos.SingleAsync(v => v.Id == video.Id)).FilePath;

        (await _client.DeleteAsync($"/api/videos/{video.Id}")).EnsureSuccessStatusCode();

        Assert.Empty((await GetSetAsync(_client, set.Id)).Videos);
        var listed = (await _client.GetFromJsonAsync<List<SetSummaryDto>>("/api/sets?stage=working", Json))!.Single(s => s.Id == set.Id);
        Assert.Equal(0, listed.VideoCount);
        Assert.Equal(0, listed.UnviewedVideoCount);
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task DeletingAnUnknownVideo_IsNotFound()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync("/api/videos/999")).StatusCode);
    }

    [Fact]
    public async Task DeviantArtInspiration_IsNotOfferedAsAFrame()
    {
        var setId = await _factory.CreateSetAsync(_client, "a fox", count: 1, addSource: form =>
        {
            form.Add(new StringContent("DeviantArt"), "sourceKind");
            form.Add(new StringContent("https://www.deviantart.com/someartist/art/Misty-Forest-123456"), "deviantArtUrl");
        });
        await WaitUntilAsync(async () => (await GetSetAsync(_client, setId)).Images.Count == 1, "image rendered");

        var frames = await FramesAsync(setId);

        Assert.DoesNotContain(frames, f => f.Key == "source");
        Assert.Single(frames);
    }

    [Fact]
    public async Task FrameOfAnotherSet_IsRefused()
    {
        await _factory.ConnectGoogleAsync();
        var set = await SetWithImagesAsync(images: 1);
        var other = await SetWithImagesAsync(images: 1);

        var response = await MakeVideoFromFramesAsync(set.Id, $"image:{other.Images[0].Id}", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain((await GetSetAsync(_client, set.Id)).Jobs, j => j.Kind == JobKind.Video);
    }

    [Fact]
    public async Task NewVideoSet_WithoutGoogleDrive_CreatesNoSet()
    {
        var response = await _client.PostAsync("/api/sets", VideoForm(withEndImage: false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await _client.GetFromJsonAsync<JsonElement>("/api/sets")).EnumerateArray());
    }

    [Fact]
    public async Task VideoJobs_StayOutOfTheImageStats()
    {
        await _factory.ConnectGoogleAsync();
        var set = await SetWithImagesAsync(images: 1);
        (await MakeVideoAsync(set.Id, set.Images[0].Id)).EnsureSuccessStatusCode();
        await WaitForVideoJobAsync(set.Id, JobStatus.Completed);

        var stats = await _client.GetFromJsonAsync<StatsDto>("/api/stats", Json);

        Assert.Equal(1, stats!.Pipeline.JobsCompletedLast30Days);
        Assert.Equal(1, stats.Pipeline.Images);
    }

    [Fact]
    public async Task DeleteSet_RemovesItsVideos()
    {
        await _factory.ConnectGoogleAsync();
        var set = await SetWithImagesAsync(images: 1);
        (await MakeVideoAsync(set.Id, set.Images[0].Id)).EnsureSuccessStatusCode();
        await WaitForVideoJobAsync(set.Id, JobStatus.Completed);
        var videoUrl = (await GetSetAsync(_client, set.Id)).Videos.Single().Url;

        (await _client.DeleteAsync($"/api/sets/{set.Id}")).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(videoUrl)).StatusCode);
    }

    [Fact]
    public async Task Options_OfferTheWorkflowsPromptTemplateAndPresets()
    {
        var options = await _client.GetFromJsonAsync<VideoOptionsDto>("/api/videos/options", Json);

        Assert.Contains("???", options!.PromptTemplate);
        Assert.Contains(options.ResolutionPresets, p => p is { Name: "896 x 512", Megapixels: 0.46, Note: null });
        Assert.Contains(options.ResolutionPresets, p => p is { Name: "864 x 480", Note: "official 480p" });
        Assert.Equal([3, 5, 6, 8, 10, 15, 20, 30], options.DurationPresets);
    }
}
