using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ArtStudio.Server.Tests.Fakes.StudioAppFactory;

namespace ArtStudio.Server.Tests;

public sealed class PromptGenerationTests : IDisposable
{
    private const string DeviationLink = "https://www.deviantart.com/someartist/art/Misty-Forest-123456";
    private static readonly byte[] SourcePng = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

    private readonly StudioAppFactory _factory = new();
    private readonly HttpClient _client;

    public PromptGenerationTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private async Task<int> CreateUploadDraftAsync()
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(""), "prompt" },
            { new StringContent("Native"), "resolution" },
            { new StringContent("2"), "count" },
            { new StringContent("true"), "draft" },
            { new StringContent("Upload"), "sourceKind" },
        };
        var file = new ByteArrayContent(SourcePng);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "image", "fox.png");
        var response = await _client.PostAsync("/api/sets", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private async Task<PromptGenerationState> StateAsync(int setId) => (await GetSetAsync(_client, setId)).PromptGeneration.State;

    private Task WaitForStateAsync(int setId, PromptGenerationState state) =>
        WaitUntilAsync(async () => await StateAsync(setId) == state, $"prompt generation {state}");

    private Task<HttpResponseMessage> GeneratePromptAsync(int setId) => _client.PostAsync($"/api/sets/{setId}/generate-prompt", null);

    [Fact]
    public async Task NewDraft_WithKey_GetsItsPromptGenerated()
    {
        await SetVisionApiKeyAsync(_client, "secret");

        var setId = await CreateUploadDraftAsync();
        await WaitForStateAsync(setId, PromptGenerationState.Done);

        var set = await GetSetAsync(_client, setId);
        Assert.Equal(_factory.Vision.Answer, set.Prompt);
        Assert.True(set.IsDraft);
        Assert.Empty(set.Jobs);
        var request = Assert.Single(_factory.Vision.Requests);
        Assert.Equal("Bearer secret", request.Authorization);
        var content = request.Body["messages"]![0]!["content"]!;
        Assert.StartsWith("Write a ready-to-use image-generation prompt", content[0]!["text"]!.GetValue<string>());
        Assert.Equal($"data:image/png;base64,{Convert.ToBase64String(SourcePng)}", content[1]!["image_url"]!["url"]!.GetValue<string>());
    }

    [Fact]
    public async Task BulkDeviantArtDraft_WithKey_IsQueuedToo()
    {
        await SetVisionApiKeyAsync(_client);

        var response = await _client.PostAsJsonAsync("/api/drafts/deviantart", new { url = DeviationLink });
        var setId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        await WaitForStateAsync(setId, PromptGenerationState.Done);
        Assert.Equal(
            $"data:image/jpeg;base64,{Convert.ToBase64String(FakeDeviantArt.JpegBytes)}",
            _factory.Vision.Requests.Single().Body["messages"]![0]!["content"]![1]!["image_url"]!["url"]!.GetValue<string>());
    }

    [Fact]
    public async Task NewDraft_WithoutKey_IsNotQueued()
    {
        var setId = await CreateUploadDraftAsync();
        await Task.Delay(200);

        Assert.Equal(PromptGenerationState.None, await StateAsync(setId));
        Assert.Empty(_factory.Vision.Requests);
        Assert.Equal(HttpStatusCode.BadRequest, (await GeneratePromptAsync(setId)).StatusCode);
    }

    [Fact]
    public async Task Failure_IsShownOnTheDraft_AndOtherDraftsStillRun()
    {
        await SetVisionApiKeyAsync(_client);
        _factory.Vision.Status = HttpStatusCode.Unauthorized;
        var failing = await CreateUploadDraftAsync();
        await WaitForStateAsync(failing, PromptGenerationState.Failed);

        _factory.Vision.Status = HttpStatusCode.OK;
        var working = await CreateUploadDraftAsync();
        await WaitForStateAsync(working, PromptGenerationState.Done);

        var failed = await GetSetAsync(_client, failing);
        Assert.Contains("API key", failed.PromptGeneration.Error);
        Assert.Equal("", failed.Prompt);

        (await GeneratePromptAsync(failing)).EnsureSuccessStatusCode();
        await WaitForStateAsync(failing, PromptGenerationState.Done);
        Assert.Null((await GetSetAsync(_client, failing)).PromptGeneration.Error);
    }

    [Fact]
    public async Task CutOffAnswer_IsKept_AndFlagged()
    {
        await SetVisionApiKeyAsync(_client);
        _factory.Vision.FinishReason = "length";

        var setId = await CreateUploadDraftAsync();
        await WaitForStateAsync(setId, PromptGenerationState.Done);

        Assert.True((await GetSetAsync(_client, setId)).PromptGeneration.Truncated);
    }

    [Fact]
    public async Task QueuingTheDraftManually_CancelsItsGeneration()
    {
        await SetVisionApiKeyAsync(_client);
        _factory.Vision.HoldAnswers = true;
        var setId = await CreateUploadDraftAsync();
        await WaitForStateAsync(setId, PromptGenerationState.Running);

        (await _client.PostAsJsonAsync($"/api/sets/{setId}/queue", new { prompt = "my own prompt", resolution = "Native", count = 1 }))
            .EnsureSuccessStatusCode();
        await WaitUntilAsync(() => Task.FromResult(_factory.Vision.CancelledRequests == 1), "vision request cancelled");
        _factory.Vision.HoldAnswers = false;
        await Task.Delay(200);

        var set = await GetSetAsync(_client, setId);
        Assert.False(set.IsDraft);
        Assert.Equal("my own prompt", set.Prompt);
        Assert.Equal(PromptGenerationState.None, set.PromptGeneration.State);
    }

    [Fact]
    public async Task DeletingTheDraft_CancelsItsGeneration()
    {
        await SetVisionApiKeyAsync(_client);
        _factory.Vision.HoldAnswers = true;
        var setId = await CreateUploadDraftAsync();
        await WaitForStateAsync(setId, PromptGenerationState.Running);

        (await _client.DeleteAsync($"/api/sets/{setId}")).EnsureSuccessStatusCode();

        await WaitUntilAsync(() => Task.FromResult(_factory.Vision.CancelledRequests == 1), "vision request cancelled");
    }

    [Fact]
    public async Task GeneratePrompt_IsRefused_WhileRunning_AndForNonDrafts()
    {
        await SetVisionApiKeyAsync(_client);
        _factory.Vision.HoldAnswers = true;
        var draftId = await CreateUploadDraftAsync();
        await WaitForStateAsync(draftId, PromptGenerationState.Running);
        var setId = await _factory.CreateSetAsync(_client, "a fox", count: 1);

        Assert.Equal(HttpStatusCode.BadRequest, (await GeneratePromptAsync(draftId)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await GeneratePromptAsync(setId)).StatusCode);
        _factory.Vision.HoldAnswers = false;
    }

    [Fact]
    public async Task ApiKey_IsStoredProtected_AndNeverReturned()
    {
        Assert.False((await _client.GetFromJsonAsync<JsonElement>("/api/vision")).GetProperty("hasApiKey").GetBoolean());

        await SetVisionApiKeyAsync(_client, "plain-secret");

        var settings = (await _client.GetFromJsonAsync<JsonElement>("/api/vision")).GetRawText();
        Assert.Contains("\"hasApiKey\":true", settings);
        Assert.DoesNotContain("plain-secret", settings);
        using var scope = _factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<StudioDbContext>().Settings.SingleAsync();
        Assert.NotNull(stored.ProtectedVisionApiKey);
        Assert.DoesNotContain("plain-secret", stored.ProtectedVisionApiKey);

        (await _client.DeleteAsync("/api/vision")).EnsureSuccessStatusCode();
        Assert.False((await _client.GetFromJsonAsync<JsonElement>("/api/vision")).GetProperty("hasApiKey").GetBoolean());
    }
}
