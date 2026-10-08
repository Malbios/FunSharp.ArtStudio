using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArtStudio.Server.Tests.Fakes;
using static ArtStudio.Server.Tests.Fakes.StudioAppFactory;

namespace ArtStudio.Server.Tests;

public sealed class PromptModifyTests : IDisposable
{
    private readonly StudioAppFactory _factory = new();
    private readonly HttpClient _client;

    public PromptModifyTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private Task<HttpResponseMessage> ModifyAsync(string text, string instructions, string? section = null) =>
        _client.PostAsJsonAsync("/api/vision/modify", new { text, instructions, section });

    private string SentText()
    {
        var content = Assert.Single(_factory.Vision.Requests).Body["messages"]![0]!["content"]!.AsArray();
        var part = Assert.Single(content);
        Assert.Equal("text", part!["type"]!.GetValue<string>());
        return part["text"]!.GetValue<string>();
    }

    private static async Task<string> ErrorAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString()!;
    }

    [Fact]
    public async Task WholePrompt_IsSentAsTextWithTheInstructions()
    {
        await SetVisionApiKeyAsync(_client, "secret");

        (await ModifyAsync("A fox.\n\nA forest.", "Make it night-time.")).EnsureSuccessStatusCode();

        Assert.Equal("Bearer secret", _factory.Vision.Requests.Single().Authorization);
        var text = SentText();
        Assert.StartsWith("Here is an image-generation prompt:", text);
        Assert.Contains("<prompt>\nA fox.\n\nA forest.\n</prompt>", text.ReplaceLineEndings("\n"));
        Assert.Contains("<instructions>\nMake it night-time.\n</instructions>", text.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task Paragraph_IsSentWithItsSection()
    {
        await SetVisionApiKeyAsync(_client);

        (await ModifyAsync("Soft watercolour.", "Make it oil paint.", "Art style")).EnsureSuccessStatusCode();

        var text = SentText().ReplaceLineEndings("\n");
        Assert.StartsWith("Here is the \"Art style\" paragraph", text);
        Assert.Contains("<paragraph>\nSoft watercolour.\n</paragraph>", text);
        Assert.Contains("<instructions>\nMake it oil paint.\n</instructions>", text);
    }

    [Fact]
    public async Task Answer_IsCleaned_AndReportsTruncation()
    {
        await SetVisionApiKeyAsync(_client);
        _factory.Vision.Answer = "A fox’s den.\r\n\r\nAt night.";
        _factory.Vision.FinishReason = "length";

        var response = await ModifyAsync("A fox's den.", "Make it night-time.");

        var answer = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("A fox's den.\n\nAt night.", answer.GetProperty("text").GetString());
        Assert.True(answer.GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public async Task WithoutKey_AsksForIt()
    {
        Assert.Contains("API key", await ErrorAsync(await ModifyAsync("A fox.", "Make it night-time.")));
        Assert.Empty(_factory.Vision.Requests);
    }

    [Fact]
    public async Task EmptyTextOrInstructions_AreRejected()
    {
        await SetVisionApiKeyAsync(_client);

        Assert.Contains("no prompt text", await ErrorAsync(await ModifyAsync("  ", "Make it night-time.")));
        Assert.Contains("Describe", await ErrorAsync(await ModifyAsync("A fox.", " ")));
        Assert.Empty(_factory.Vision.Requests);
    }

    [Fact]
    public async Task UnreachableServer_IsAReadableError()
    {
        await SetVisionApiKeyAsync(_client);
        _factory.Vision.Unreachable = true;

        Assert.Contains("SSH tunnel", await ErrorAsync(await ModifyAsync("A fox.", "Make it night-time.")));
    }
}
