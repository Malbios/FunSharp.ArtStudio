using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArtStudio.Server.Api;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Tests.Fakes;
using static ArtStudio.Server.Tests.Fakes.StudioAppFactory;

namespace ArtStudio.Server.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly StudioAppFactory _factory = new();
    private readonly HttpClient _client;

    public SettingsTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Defaults_HaveNotificationsOffAndOutputInDataDirectory()
    {
        var settings = await _client.GetFromJsonAsync<SettingsDto>("/api/settings", Json);

        Assert.False(settings!.NotifyOnJobDone);
        Assert.False(settings.NotifyOnQueueEmpty);
        Assert.StartsWith(_factory.DataDirectory, settings.OutputDirectory);
    }

    [Fact]
    public async Task ChangedOutputDirectory_IsUsedForNewImages()
    {
        var customOutput = Path.Combine(_factory.DataDirectory, "custom-output");
        var current = await _client.GetFromJsonAsync<SettingsDto>("/api/settings", Json);
        (await _client.PutAsJsonAsync("/api/settings", current! with { OutputDirectory = customOutput })).EnsureSuccessStatusCode();

        var setId = await _factory.CreateSetAsync(_client, "fox", count: 1);
        await WaitUntilAsync(async () => (await GetSetAsync(_client, setId)).Jobs.Single().Status == JobStatus.Completed, "job done");

        Assert.Single(Directory.GetFiles(Path.Combine(customOutput, "generated", setId.ToString())));
    }

    [Theory]
    [InlineData("not a url", "C:\\Images")]
    [InlineData("http://localhost:8188", "relative\\path")]
    public async Task InvalidSettings_AreRejected(string url, string outputDirectory)
    {
        var response = await _client.PutAsJsonAsync("/api/settings", new SettingsDto(url, outputDirectory, false, false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BuildingBlocks_CanBeAddedEditedReorderedAndDeleted()
    {
        var first = await AddBlockAsync("", "cinematic lighting");
        var second = await AddBlockAsync("Style", "oil painting, thick brush strokes");
        Assert.Equal("cinematic lighting", first.GetProperty("label").GetString());

        (await _client.PutAsJsonAsync($"/api/building-blocks/{first.GetProperty("id")}", new { label = "Light", text = "soft light" }))
            .EnsureSuccessStatusCode();
        (await _client.PostAsJsonAsync("/api/building-blocks/reorder", new { ids = new[] { second.GetProperty("id").GetInt32(), first.GetProperty("id").GetInt32() } }))
            .EnsureSuccessStatusCode();

        var blocks = await _client.GetFromJsonAsync<JsonElement>("/api/building-blocks");
        Assert.Equal(["Style", "Light"], blocks.EnumerateArray().Select(b => b.GetProperty("label").GetString()));

        (await _client.DeleteAsync($"/api/building-blocks/{second.GetProperty("id")}")).EnsureSuccessStatusCode();
        Assert.Equal(1, (await _client.GetFromJsonAsync<JsonElement>("/api/building-blocks")).GetArrayLength());
    }

    [Fact]
    public async Task BuildingBlockText_KeepsLeadingWhitespace()
    {
        var block = await AddBlockAsync("", "  , cinematic lighting");

        Assert.Equal("  , cinematic lighting", block.GetProperty("text").GetString());
        Assert.Equal(", cinematic lighting", block.GetProperty("label").GetString());
    }

    private async Task<JsonElement> AddBlockAsync(string label, string text)
    {
        var response = await _client.PostAsJsonAsync("/api/building-blocks", new { label, text });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
