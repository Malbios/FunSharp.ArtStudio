using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Tests.Fakes;
using static ArtStudio.Server.Tests.Fakes.StudioAppFactory;

namespace ArtStudio.Server.Tests;

public sealed class QueueTests : IDisposable
{
    private readonly StudioAppFactory _factory = new();
    private readonly HttpClient _client;

    public QueueTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private Task WaitForJobStatusAsync(int setId, JobStatus status) =>
        WaitUntilAsync(async () => (await GetSetAsync(_client, setId)).Jobs.Any(j => j.Status == status), $"set {setId} job {status}");

    [Fact]
    public async Task CreateSet_TextOnly_GeneratesRequestedImages()
    {
        var setId = await _factory.CreateSetAsync(_client, "a red fox", count: 2);

        await WaitForJobStatusAsync(setId, JobStatus.Completed);

        var set = await GetSetAsync(_client, setId);
        Assert.Equal(2, set.Images.Count);
        Assert.Equal(2, set.Jobs.Single().CompletedCount);
        Assert.Equal(["a red fox", "a red fox"], _factory.Comfy.SubmittedPrompts);
        Assert.Equal(FakeComfyServer.PngBytes, await _client.GetByteArrayAsync(set.Images[0].Url));
        Assert.NotEqual(set.Images[0].Seed, set.Images[1].Seed);
    }

    [Fact]
    public async Task ImageSeed_IsReturnedExactlyAsText()
    {
        var setId = await _factory.CreateSetAsync(_client, "fox", count: 1);
        await WaitForJobStatusAsync(setId, JobStatus.Completed);

        var json = await _client.GetStringAsync($"/api/sets/{setId}");
        var seed = (await GetSetAsync(_client, setId)).Images.Single().Seed;

        Assert.Contains($"\"seed\":\"{seed}\"", json);
        var savedFile = Directory.GetFiles(Path.Combine(ImagesDirectory, "generated", setId.ToString())).Single();
        Assert.EndsWith($"-{seed}.png", savedFile);
    }

    [Fact]
    public async Task CreateSet_WithUpload_StoresAndServesSourceImage()
    {
        byte[] sourceBytes = [1, 2, 3, 4];

        var setId = await _factory.CreateSetAsync(_client, "fox", count: 1, addSource: form =>
        {
            form.Add(new StringContent("Upload"), "sourceKind");
            var file = new ByteArrayContent(sourceBytes);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(file, "image", "fox.jpg");
        });

        var set = await GetSetAsync(_client, setId);
        Assert.Equal(SourceKind.Upload, set.SourceKind);
        Assert.Equal(sourceBytes, await _client.GetByteArrayAsync(set.SourceImageUrl));
    }

    [Fact]
    public async Task CreateSet_UnknownResolution_ReturnsError()
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent("fox"), "prompt" },
            { new StringContent("Gigantic"), "resolution" },
            { new StringContent("2"), "count" },
        };

        var response = await _client.PostAsync("/api/sets", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Gigantic", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task MoreImages_AddsJobToSameSet()
    {
        var setId = await _factory.CreateSetAsync(_client, "fox", count: 1);
        await WaitForJobStatusAsync(setId, JobStatus.Completed);

        var response = await _client.PostAsJsonAsync($"/api/sets/{setId}/more", new { count = 2 });
        response.EnsureSuccessStatusCode();

        await WaitUntilAsync(async () => (await GetSetAsync(_client, setId)).Images.Count == 3, "three images");
        Assert.Equal(2, (await GetSetAsync(_client, setId)).Jobs.Count);
    }

    [Fact]
    public async Task RequeueWithChangedPrompt_AddsImagesToSameSetWithTheirOwnPrompt()
    {
        var setId = await _factory.CreateSetAsync(_client, "a fox, photo", count: 1, resolution: "Native");
        await WaitForJobStatusAsync(setId, JobStatus.Completed);

        (await _client.PostAsJsonAsync($"/api/sets/{setId}/more",
            new { count = 2, prompt = "a fox, watercolor", resolution = "Wide" })).EnsureSuccessStatusCode();
        await WaitUntilAsync(async () => (await GetSetAsync(_client, setId)).Images.Count == 3, "three images");

        var set = await GetSetAsync(_client, setId);
        Assert.Single((await _client.GetFromJsonAsync<JsonElement>("/api/sets")).EnumerateArray());
        Assert.Equal(["a fox, photo", "a fox, watercolor", "a fox, watercolor"], set.Images.Select(i => i.Prompt));
        Assert.Equal(["Native", "Wide", "Wide"], set.Images.Select(i => i.Resolution));
        Assert.Equal(["a fox, photo", "a fox, watercolor"], set.Jobs.Select(j => j.Prompt));
        Assert.Equal("a fox, watercolor", set.Prompt);
        Assert.Equal("Wide", set.Resolution);
        Assert.Equal(["a fox, photo", "a fox, watercolor", "a fox, watercolor"], _factory.Comfy.SubmittedPrompts);
    }

    [Fact]
    public async Task MoreImages_WithoutPrompt_ReusesLatestPromptAndResolution()
    {
        var setId = await _factory.CreateSetAsync(_client, "a fox", count: 1);
        await WaitForJobStatusAsync(setId, JobStatus.Completed);
        (await _client.PostAsJsonAsync($"/api/sets/{setId}/more",
            new { count = 1, prompt = "an owl", resolution = "Tall" })).EnsureSuccessStatusCode();

        (await _client.PostAsJsonAsync($"/api/sets/{setId}/more", new { count = 1 })).EnsureSuccessStatusCode();

        await WaitUntilAsync(async () => (await GetSetAsync(_client, setId)).Images.Count == 3, "three images");
        var latest = (await GetSetAsync(_client, setId)).Images.Last();
        Assert.Equal("an owl", latest.Prompt);
        Assert.Equal("Tall", latest.Resolution);
    }

    [Theory]
    [InlineData("   ", "Native")]
    [InlineData("an owl", "Gigantic")]
    public async Task MoreImages_InvalidPromptOrResolution_ReturnsBadRequest(string prompt, string resolution)
    {
        var setId = await _factory.CreateSetAsync(_client, "a fox", count: 1);

        var response = await _client.PostAsJsonAsync($"/api/sets/{setId}/more", new { count = 1, prompt, resolution });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single((await GetSetAsync(_client, setId)).Jobs);
    }

    [Fact]
    public async Task Failure_PausesQueue_AndRetryResumes()
    {
        _factory.Comfy.FailRuns = true;
        var failingSet = await _factory.CreateSetAsync(_client, "fails", count: 1);
        await WaitForJobStatusAsync(failingSet, JobStatus.Failed);
        var waitingSet = await _factory.CreateSetAsync(_client, "waits", count: 1);
        await Task.Delay(200);

        var queue = await GetQueueAsync(_client);
        Assert.True(queue.Paused);
        Assert.Equal(JobStatus.Queued, (await GetSetAsync(_client, waitingSet)).Jobs.Single().Status);
        Assert.NotNull((await GetSetAsync(_client, failingSet)).Jobs.Single().Error);

        _factory.Comfy.FailRuns = false;
        var failedJobId = (await GetSetAsync(_client, failingSet)).Jobs.Single().Id;
        (await _client.PostAsync($"/api/jobs/{failedJobId}/retry", null)).EnsureSuccessStatusCode();

        await WaitForJobStatusAsync(waitingSet, JobStatus.Completed);
        Assert.Equal(JobStatus.Completed, (await GetSetAsync(_client, failingSet)).Jobs.Single().Status);
        Assert.False((await GetQueueAsync(_client)).Paused);
        Assert.Equal(["fails", "fails"], _factory.Comfy.SubmittedPrompts.Take(2));
        Assert.Equal("waits", _factory.Comfy.SubmittedPrompts.Last());
    }

    [Fact]
    public async Task CancelQueuedJob_IsNeverRun()
    {
        _factory.Comfy.HoldRuns = true;
        var runningSet = await _factory.CreateSetAsync(_client, "first", count: 1);
        await WaitForJobStatusAsync(runningSet, JobStatus.Running);
        var queuedSet = await _factory.CreateSetAsync(_client, "second", count: 1);
        var queuedJobId = (await GetSetAsync(_client, queuedSet)).Jobs.Single().Id;

        (await _client.PostAsync($"/api/jobs/{queuedJobId}/cancel", null)).EnsureSuccessStatusCode();
        _factory.Comfy.HoldRuns = false;

        await WaitForJobStatusAsync(runningSet, JobStatus.Completed);
        Assert.Equal(JobStatus.Cancelled, (await GetSetAsync(_client, queuedSet)).Jobs.Single().Status);
        Assert.DoesNotContain("second", _factory.Comfy.SubmittedPrompts);
    }

    private Task<HttpResponseMessage> MoveAsync(int jobId, int offset) =>
        _client.PostAsJsonAsync($"/api/jobs/{jobId}/move", new { offset });

    private async Task<int> SingleJobIdAsync(int setId) => (await GetSetAsync(_client, setId)).Jobs.Single().Id;

    [Fact]
    public async Task MovedJob_RunsInItsNewPlace()
    {
        (await _client.PostAsync("/api/queue/pause", null)).EnsureSuccessStatusCode();
        var first = await SingleJobIdAsync(await _factory.CreateSetAsync(_client, "first", count: 1));
        var second = await SingleJobIdAsync(await _factory.CreateSetAsync(_client, "second", count: 1));
        var third = await SingleJobIdAsync(await _factory.CreateSetAsync(_client, "third", count: 1));

        (await MoveAsync(third, -1)).EnsureSuccessStatusCode();

        Assert.Equal([first, third, second], (await GetQueueAsync(_client)).Active.Select(j => j.Id));
        (await _client.PostAsync("/api/queue/resume", null)).EnsureSuccessStatusCode();
        await WaitUntilAsync(() => Task.FromResult(_factory.Comfy.SubmittedPrompts.Count == 3), "all three rendered");
        Assert.Equal(["first", "third", "second"], _factory.Comfy.SubmittedPrompts);
    }

    [Fact]
    public async Task MovingPastTheEnds_OrARunningJob_IsRefused()
    {
        _factory.Comfy.HoldRuns = true;
        var runningSet = await _factory.CreateSetAsync(_client, "running", count: 1);
        await WaitForJobStatusAsync(runningSet, JobStatus.Running);
        var first = await SingleJobIdAsync(await _factory.CreateSetAsync(_client, "first", count: 1));
        var last = await SingleJobIdAsync(await _factory.CreateSetAsync(_client, "last", count: 1));

        Assert.Equal(HttpStatusCode.BadRequest, (await MoveAsync(first, -1)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await MoveAsync(last, 1)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await MoveAsync(await SingleJobIdAsync(runningSet), 1)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await MoveAsync(first, 2)).StatusCode);
        _factory.Comfy.HoldRuns = false;
    }

    [Fact]
    public async Task CancelRunningJob_StopsAndCleansUpComfy()
    {
        _factory.Comfy.HoldRuns = true;
        var setId = await _factory.CreateSetAsync(_client, "slow", count: 3);
        await WaitUntilAsync(() => Task.FromResult(_factory.Comfy.SubmittedPrompts.Count == 1), "prompt submitted");
        var jobId = (await GetSetAsync(_client, setId)).Jobs.Single().Id;

        (await _client.PostAsync($"/api/jobs/{jobId}/cancel", null)).EnsureSuccessStatusCode();

        await WaitForJobStatusAsync(setId, JobStatus.Cancelled);
        Assert.Equal(["prompt-1"], _factory.Comfy.DeletedPromptIds);
        Assert.Equal(1, _factory.Comfy.InterruptCount);
        Assert.False((await GetQueueAsync(_client)).Paused);
    }

    [Fact]
    public async Task CancelFinishedJob_ReturnsError()
    {
        var setId = await _factory.CreateSetAsync(_client, "fox", count: 1);
        await WaitForJobStatusAsync(setId, JobStatus.Completed);
        var jobId = (await GetSetAsync(_client, setId)).Jobs.Single().Id;

        var response = await _client.PostAsync($"/api/jobs/{jobId}/cancel", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private string ImagesDirectory => Path.Combine(_factory.DataDirectory, "images");

    [Fact]
    public async Task DeleteSet_RemovesDatabaseRowsButKeepsFiles()
    {
        var setId = await _factory.CreateSetAsync(_client, "fox", count: 2, addSource: form =>
        {
            form.Add(new StringContent("Upload"), "sourceKind");
            var file = new ByteArrayContent([1, 2, 3]);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "image", "fox.png");
        });
        await WaitForJobStatusAsync(setId, JobStatus.Completed);
        var imageUrl = (await GetSetAsync(_client, setId)).Images[0].Url;
        var filesBefore = Directory.GetFiles(ImagesDirectory, "*", SearchOption.AllDirectories);

        var response = await _client.DeleteAsync($"/api/sets/{setId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/sets/{setId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(imageUrl)).StatusCode);
        Assert.DoesNotContain((await _client.GetFromJsonAsync<JsonElement>("/api/sets")).EnumerateArray(),
            s => s.GetProperty("id").GetInt32() == setId);
        Assert.Equal(3, filesBefore.Length);
        Assert.All(filesBefore, path => Assert.True(File.Exists(path)));
    }

    [Fact]
    public async Task DeleteSet_NeverReusesItsIdForNewSets()
    {
        var deletedId = await _factory.CreateSetAsync(_client, "fox", count: 1);
        await WaitForJobStatusAsync(deletedId, JobStatus.Completed);
        var keptFile = Directory.GetFiles(Path.Combine(ImagesDirectory, "generated", deletedId.ToString())).Single();
        (await _client.DeleteAsync($"/api/sets/{deletedId}")).EnsureSuccessStatusCode();

        var newId = await _factory.CreateSetAsync(_client, "owl", count: 1);
        await WaitForJobStatusAsync(newId, JobStatus.Completed);

        Assert.True(newId > deletedId);
        Assert.Single(Directory.GetFiles(Path.Combine(ImagesDirectory, "generated", deletedId.ToString())), keptFile);
    }

    [Fact]
    public async Task DeleteSet_DropsQueuedJobAndQueueContinues()
    {
        _factory.Comfy.HoldRuns = true;
        var runningSet = await _factory.CreateSetAsync(_client, "first", count: 1);
        await WaitForJobStatusAsync(runningSet, JobStatus.Running);
        var deletedSet = await _factory.CreateSetAsync(_client, "deleted", count: 1);
        var laterSet = await _factory.CreateSetAsync(_client, "later", count: 1);

        (await _client.DeleteAsync($"/api/sets/{deletedSet}")).EnsureSuccessStatusCode();
        _factory.Comfy.HoldRuns = false;

        await WaitForJobStatusAsync(laterSet, JobStatus.Completed);
        Assert.Equal(["first", "later"], _factory.Comfy.SubmittedPrompts);
    }

    [Fact]
    public async Task DeleteSet_CancelsRunningJobOnComfy()
    {
        _factory.Comfy.HoldRuns = true;
        var setId = await _factory.CreateSetAsync(_client, "slow", count: 2);
        await WaitUntilAsync(() => Task.FromResult(_factory.Comfy.SubmittedPrompts.Count == 1), "prompt submitted");

        var response = await _client.DeleteAsync($"/api/sets/{setId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(["prompt-1"], _factory.Comfy.DeletedPromptIds);
        Assert.Equal(1, _factory.Comfy.InterruptCount);
        var queue = await GetQueueAsync(_client);
        Assert.False(queue.Paused);
        Assert.Empty(queue.Active);
    }

    [Fact]
    public async Task DeleteUnknownSet_ReturnsNotFound()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync("/api/sets/999")).StatusCode);
    }
}
