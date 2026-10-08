using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Tests.Fakes;
using ArtStudio.Server.Vision;
using static ArtStudio.Server.Tests.Fakes.StudioAppFactory;

namespace ArtStudio.Server.Tests;

public sealed class PromptModifyTests : IDisposable
{
    private const string FourParagraphs = "A fox.\n\nA forest.\n\nEye level.\n\nSoft watercolour.";

    private readonly StudioAppFactory _factory = new();
    private readonly HttpClient _client;

    public PromptModifyTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private Task<int> CreateSetAsync() => _factory.CreateSetAsync(_client, "Saved prompt.");

    private Task<HttpResponseMessage> ModifyAsync(
        int setId, string prompt, string instructions, int? paragraphIndex = null, string? section = null) =>
        _client.PostAsJsonAsync($"/api/sets/{setId}/modify-prompt", new { prompt, instructions, paragraphIndex, section });

    private Task WaitForStateAsync(int setId, PromptGenerationState state) =>
        WaitUntilAsync(async () => (await GetSetAsync(_client, setId)).PromptGeneration.State == state, $"modification {state}");

    private string SentText()
    {
        var content = Assert.Single(_factory.Vision.Requests).Body["messages"]![0]!["content"]!.AsArray();
        var part = Assert.Single(content);
        Assert.Equal("text", part!["type"]!.GetValue<string>());
        return part["text"]!.GetValue<string>().ReplaceLineEndings("\n");
    }

    private static async Task<string> ErrorAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString()!;
    }

    [Fact]
    public async Task WholePrompt_IsQueued_AndBecomesTheSetsPrompt()
    {
        await SetVisionApiKeyAsync(_client, "secret");
        var setId = await CreateSetAsync();
        _factory.Vision.Answer = "A fox at night.\n\nA dark forest.";

        (await ModifyAsync(setId, "A fox.\n\nA forest.", "Make it night-time.")).EnsureSuccessStatusCode();
        await WaitForStateAsync(setId, PromptGenerationState.Done);

        var set = await GetSetAsync(_client, setId);
        Assert.Equal("A fox at night.\n\nA dark forest.", set.Prompt);
        Assert.Equal(set.Prompt, set.PromptGeneration.Text);
        Assert.Equal(PromptGenerationKind.Modify, set.PromptGeneration.Kind);
        Assert.Equal("Bearer secret", _factory.Vision.Requests.Single().Authorization);
        var text = SentText();
        Assert.StartsWith("Here is an image-generation prompt:", text);
        Assert.Contains("<prompt>\nA fox.\n\nA forest.\n</prompt>", text);
        Assert.Contains("<instructions>\nMake it night-time.\n</instructions>", text);
    }

    [Fact]
    public async Task Paragraph_ReplacesOnlyThatParagraph()
    {
        await SetVisionApiKeyAsync(_client);
        var setId = await CreateSetAsync();
        _factory.Vision.Answer = "Thick oil paint.\n\nWith visible strokes.";

        (await ModifyAsync(setId, FourParagraphs, "Make it oil paint.", 3, "Art style")).EnsureSuccessStatusCode();
        await WaitForStateAsync(setId, PromptGenerationState.Done);

        Assert.Equal("A fox.\n\nA forest.\n\nEye level.\n\nThick oil paint. With visible strokes.", (await GetSetAsync(_client, setId)).Prompt);
        var text = SentText();
        Assert.StartsWith("Here is the \"Art style\" paragraph", text);
        Assert.Contains("<paragraph>\nSoft watercolour.\n</paragraph>", text);
        Assert.DoesNotContain("A forest.", text);
    }

    [Fact]
    public async Task CutOffAnswer_IsReported()
    {
        await SetVisionApiKeyAsync(_client);
        var setId = await CreateSetAsync();
        _factory.Vision.FinishReason = "length";

        (await ModifyAsync(setId, "A fox.", "Make it night-time.")).EnsureSuccessStatusCode();
        await WaitForStateAsync(setId, PromptGenerationState.Done);

        Assert.True((await GetSetAsync(_client, setId)).PromptGeneration.Truncated);
    }

    [Fact]
    public async Task Failure_IsShownAsAFailedModification()
    {
        await SetVisionApiKeyAsync(_client);
        var setId = await CreateSetAsync();
        _factory.Vision.Unreachable = true;

        (await ModifyAsync(setId, "A fox.", "Make it night-time.")).EnsureSuccessStatusCode();
        await WaitForStateAsync(setId, PromptGenerationState.Failed);

        var generation = (await GetSetAsync(_client, setId)).PromptGeneration;
        Assert.Equal(PromptGenerationKind.Modify, generation.Kind);
        Assert.Contains("SSH tunnel", generation.Error);
        Assert.Equal("Saved prompt.", (await GetSetAsync(_client, setId)).Prompt);
    }

    [Fact]
    public async Task InvalidRequests_AreRejected()
    {
        var setId = await CreateSetAsync();
        Assert.Contains("API key", await ErrorAsync(await ModifyAsync(setId, "A fox.", "Make it night-time.")));

        await SetVisionApiKeyAsync(_client);
        Assert.Contains("no prompt text", await ErrorAsync(await ModifyAsync(setId, "  ", "Make it night-time.")));
        Assert.Contains("Describe", await ErrorAsync(await ModifyAsync(setId, "A fox.", " ")));
        Assert.Contains("paragraph", await ErrorAsync(await ModifyAsync(setId, FourParagraphs, "Oil paint.", 4, "Art style")));
        Assert.Empty(_factory.Vision.Requests);
    }

    [Fact]
    public async Task WhilePending_AnotherRequestIsRejected()
    {
        await SetVisionApiKeyAsync(_client);
        var setId = await CreateSetAsync();
        _factory.Vision.HoldAnswers = true;

        (await ModifyAsync(setId, "A fox.", "Make it night-time.")).EnsureSuccessStatusCode();

        Assert.Contains("already", await ErrorAsync(await ModifyAsync(setId, "A fox.", "Make it day.")));
        _factory.Vision.HoldAnswers = false;
        await WaitForStateAsync(setId, PromptGenerationState.Done);
    }

    [Fact]
    public void Apply_KeepsTheOtherParagraphs()
    {
        Assert.Equal("A fox's den.\n\nB", PromptModifier.Apply("A\n\nB", 0, "A fox’s den.\r\n\r\n"));
        Assert.Equal("New whole prompt.", PromptModifier.Apply("A\n\nB", null, "New whole prompt."));
    }
}
