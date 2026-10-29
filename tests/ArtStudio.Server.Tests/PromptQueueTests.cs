using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ArtStudio.Server.Api;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Tests.Fakes;
using static ArtStudio.Server.Tests.Fakes.StudioAppFactory;

namespace ArtStudio.Server.Tests;

public sealed class PromptQueueTests : IDisposable
{
    private readonly StudioAppFactory _factory = new();
    private readonly HttpClient _client;

    public PromptQueueTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private async Task<int> CreateDraftAsync(byte marker)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(""), "prompt" },
            { new StringContent("Native"), "resolution" },
            { new StringContent("2"), "count" },
            { new StringContent("true"), "draft" },
            { new StringContent("Upload"), "sourceKind" },
        };
        var file = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, marker]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "image", "fox.png");
        var response = await _client.PostAsync("/api/sets", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private async Task<List<PromptQueueItemDto>> QueueAsync() =>
        (await _client.GetFromJsonAsync<List<PromptQueueItemDto>>("/api/prompt-queue", Json))!;

    private async Task<int[]> QueuedIdsAsync() => [.. (await QueueAsync()).Select(item => item.SetId)];

    private Task<HttpResponseMessage> MoveAsync(int setId, string move) =>
        _client.PostAsJsonAsync($"/api/prompt-queue/{setId}/move", new { move });

    private Task<HttpResponseMessage> CancelAsync(int setId) => _client.PostAsync($"/api/prompt-queue/{setId}/cancel", null);

    private Task WaitForStateAsync(int setId, PromptGenerationState state) =>
        WaitUntilAsync(async () => (await GetSetAsync(_client, setId)).PromptGeneration.State == state, $"prompt {state}");

    private byte[] DescribedMarkers() => [.. _factory.Vision.Requests.Select(request =>
        Convert.FromBase64String(request.Body["messages"]![0]!["content"]![1]!["image_url"]!["url"]!.GetValue<string>()
            .Split(',')[1])[^1])];

    /// <summary>Draft 1 is running; 2 and 3 wait as automatic prompts; 4 is clicked for, so it waits ahead of them.</summary>
    private async Task<(int Running, int First, int Second, int Clicked)> QueueFourAsync()
    {
        var clicked = await CreateDraftAsync(4);
        await SetVisionApiKeyAsync(_client);
        _factory.Vision.HoldAnswers = true;
        var running = await CreateDraftAsync(1);
        await WaitForStateAsync(running, PromptGenerationState.Running);
        var first = await CreateDraftAsync(2);
        var second = await CreateDraftAsync(3);
        (await _client.PostAsync($"/api/sets/{clicked}/generate-prompt", null)).EnsureSuccessStatusCode();
        return (running, first, second, clicked);
    }

    [Fact]
    public async Task Queue_ShowsTheRunningPromptFirst_ThenTheWaitingOnesInRunOrder()
    {
        var (running, first, second, clicked) = await QueueFourAsync();

        var queue = await QueueAsync();

        Assert.Equal([running, clicked, first, second], queue.Select(item => item.SetId));
        Assert.Equal(
            [PromptGenerationState.Running, PromptGenerationState.Queued, PromptGenerationState.Queued, PromptGenerationState.Queued],
            queue.Select(item => item.State));
        Assert.All(queue, item => Assert.True(item.IsDraft));
        Assert.Equal($"/api/sets/{clicked}/source", queue[1].SourceImageUrl);
        _factory.Vision.HoldAnswers = false;
    }

    [Fact]
    public async Task MovedPrompts_RunInTheirNewOrder()
    {
        var (running, first, second, clicked) = await QueueFourAsync();

        (await MoveAsync(second, "Top")).EnsureSuccessStatusCode();
        Assert.Equal([running, second, clicked, first], await QueuedIdsAsync());
        (await MoveAsync(second, "Down")).EnsureSuccessStatusCode();
        Assert.Equal([running, clicked, second, first], await QueuedIdsAsync());
        (await MoveAsync(clicked, "Bottom")).EnsureSuccessStatusCode();
        Assert.Equal([running, second, first, clicked], await QueuedIdsAsync());
        (await MoveAsync(first, "Up")).EnsureSuccessStatusCode();
        Assert.Equal([running, first, second, clicked], await QueuedIdsAsync());

        _factory.Vision.HoldAnswers = false;
        await WaitForStateAsync(clicked, PromptGenerationState.Done);
        Assert.Equal<byte>([1, 2, 3, 4], DescribedMarkers());
    }

    [Fact]
    public async Task Moving_IsRefused_PastTheEnds_AndForTheRunningPrompt()
    {
        var (running, _, second, clicked) = await QueueFourAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await MoveAsync(clicked, "Up")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await MoveAsync(second, "Bottom")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await MoveAsync(running, "Down")).StatusCode);
        _factory.Vision.HoldAnswers = false;
    }

    [Fact]
    public async Task CancellingAWaitingPrompt_TakesItOutOfTheQueue()
    {
        var (running, first, second, clicked) = await QueueFourAsync();

        (await CancelAsync(first)).EnsureSuccessStatusCode();

        Assert.Equal([running, clicked, second], await QueuedIdsAsync());
        Assert.Equal(PromptGenerationState.None, (await GetSetAsync(_client, first)).PromptGeneration.State);
        _factory.Vision.HoldAnswers = false;
        await WaitForStateAsync(second, PromptGenerationState.Done);
        Assert.Equal<byte>([1, 4, 3], DescribedMarkers());
    }

    [Fact]
    public async Task CancellingTheRunningPrompt_StopsIt()
    {
        var (running, _, _, _) = await QueueFourAsync();

        (await CancelAsync(running)).EnsureSuccessStatusCode();

        await WaitUntilAsync(() => Task.FromResult(_factory.Vision.CancelledRequests == 1), "the vision request cancelled");
        Assert.Equal(PromptGenerationState.None, (await GetSetAsync(_client, running)).PromptGeneration.State);
        Assert.DoesNotContain(running, await QueuedIdsAsync());
        _factory.Vision.HoldAnswers = false;
    }

    [Fact]
    public async Task Cancelling_IsRefused_WhenNothingIsQueued()
    {
        var setId = await CreateDraftAsync(1);

        Assert.Equal(HttpStatusCode.BadRequest, (await CancelAsync(setId)).StatusCode);
    }
}
