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
    public async Task EditAsNewSet_CopiesSourceIntoNewSet()
    {
        var originalId = await _factory.CreateSetAsync(_client, "fox", count: 1, addSource: form =>
        {
            form.Add(new StringContent("Paste"), "sourceKind");
            var file = new ByteArrayContent([9, 9]);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "image", "clipboard.png");
        });

        var copyId = await _factory.CreateSetAsync(_client, "fox, edited", count: 1,
            addSource: form => form.Add(new StringContent(originalId.ToString()), "basedOnSetId"));

        var copy = await GetSetAsync(_client, copyId);
        Assert.NotEqual(originalId, copyId);
        Assert.Equal(SourceKind.Paste, copy.SourceKind);
        Assert.Equal([9, 9], await _client.GetByteArrayAsync(copy.SourceImageUrl));
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

    [Fact]
    public async Task SelectImage_StoresSelection()
    {
        var setId = await _factory.CreateSetAsync(_client, "fox", count: 2);
        await WaitForJobStatusAsync(setId, JobStatus.Completed);
        var imageId = (await GetSetAsync(_client, setId)).Images[1].Id;

        (await _client.PostAsJsonAsync($"/api/sets/{setId}/select", new { imageId })).EnsureSuccessStatusCode();

        Assert.Equal(imageId, (await GetSetAsync(_client, setId)).SelectedImageId);
    }
}
