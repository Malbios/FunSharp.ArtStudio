using System.Net;
using System.Net.Http.Headers;
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

    [Fact]
    public async Task Presets_CanBeAddedAndEdited_NextToTextBlocks()
    {
        var text = await AddBlockAsync("Light", "soft light");
        var response = await _client.PostAsJsonAsync("/api/building-blocks",
            new { label = " Oil ", kind = "Preset", artStyle = " Thick oil paint. ", resolution = "Portrait", imageCount = 4 });
        response.EnsureSuccessStatusCode();
        var preset = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Preset", preset.GetProperty("kind").GetString());
        Assert.Equal(("Oil", "Thick oil paint.", "Portrait", 4), (
            preset.GetProperty("label").GetString(),
            preset.GetProperty("artStyle").GetString(),
            preset.GetProperty("resolution").GetString(),
            preset.GetProperty("imageCount").GetInt32()));

        (await _client.PutAsJsonAsync($"/api/building-blocks/{preset.GetProperty("id")}",
            new { label = "Oil", kind = "Preset", artStyle = "", resolution = "", imageCount = 2 })).EnsureSuccessStatusCode();

        var blocks = (await _client.GetFromJsonAsync<JsonElement>("/api/building-blocks")).EnumerateArray().ToList();
        var updated = blocks.Single(b => b.GetProperty("id").GetInt32() == preset.GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("artStyle").ValueKind);
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("resolution").ValueKind);
        Assert.Equal(2, updated.GetProperty("imageCount").GetInt32());
        var unchanged = blocks.Single(b => b.GetProperty("id").GetInt32() == text.GetProperty("id").GetInt32());
        Assert.Equal(("Text", "soft light"), (unchanged.GetProperty("kind").GetString(), unchanged.GetProperty("text").GetString()));
    }

    [Theory]
    [InlineData("", "Thick oil paint.", null, null)]
    [InlineData("Empty", "  ", null, null)]
    [InlineData("Huge", null, "Huge", null)]
    [InlineData("None", null, null, 0)]
    [InlineData("Many", null, null, 101)]
    public async Task InvalidPresets_AreRejected(string label, string? artStyle, string? resolution, int? imageCount)
    {
        var response = await _client.PostAsJsonAsync("/api/building-blocks",
            new { label, kind = "Preset", artStyle, resolution, imageCount });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<JsonElement> AddPresetAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/building-blocks", new { label = "Oil", kind = "Preset", imageCount = 2 });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private Task<HttpResponseMessage> UploadImageAsync(JsonElement block, byte[] bytes, string contentType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { content, "image", "example" } };
        return _client.PutAsync($"/api/building-blocks/{block.GetProperty("id")}/image", form);
    }

    [Fact]
    public async Task PresetImage_IsStoredServedReplacedAndRemoved()
    {
        var preset = await AddPresetAsync();
        Assert.Equal(JsonValueKind.Null, preset.GetProperty("imageUrl").ValueKind);

        var uploaded = await (await UploadImageAsync(preset, [1, 2, 3], "image/png")).Content.ReadFromJsonAsync<JsonElement>();
        var firstUrl = uploaded.GetProperty("imageUrl").GetString()!;
        var served = await _client.GetAsync(firstUrl);
        Assert.Equal("image/png", served.Content.Headers.ContentType?.MediaType);
        Assert.Equal([1, 2, 3], await served.Content.ReadAsByteArrayAsync());

        await Task.Delay(20);
        var replaced = await (await UploadImageAsync(preset, [4, 5], "image/jpeg")).Content.ReadFromJsonAsync<JsonElement>();
        var secondUrl = replaced.GetProperty("imageUrl").GetString()!;
        Assert.NotEqual(firstUrl, secondUrl);
        Assert.Equal([4, 5], await _client.GetByteArrayAsync(secondUrl));
        var presetsDirectory = Path.Combine(_factory.DataDirectory, "presets");
        Assert.Single(Directory.GetFiles(presetsDirectory));

        var removed = await (await _client.DeleteAsync($"/api/building-blocks/{preset.GetProperty("id")}/image"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, removed.GetProperty("imageUrl").ValueKind);
        Assert.Empty(Directory.GetFiles(presetsDirectory));
    }

    [Fact]
    public async Task DeletingAPreset_DeletesItsImage()
    {
        var preset = await AddPresetAsync();
        (await UploadImageAsync(preset, [1, 2, 3], "image/webp")).EnsureSuccessStatusCode();

        (await _client.DeleteAsync($"/api/building-blocks/{preset.GetProperty("id")}")).EnsureSuccessStatusCode();

        Assert.Empty(Directory.GetFiles(Path.Combine(_factory.DataDirectory, "presets")));
    }

    private static int Id(JsonElement block) => block.GetProperty("id").GetInt32();

    private static int[] PresetIds(JsonElement block) =>
        block.GetProperty("presetIds").EnumerateArray().Select(id => id.GetInt32()).ToArray();

    private async Task<JsonElement[]> BlocksAsync() =>
        (await _client.GetFromJsonAsync<JsonElement[]>("/api/building-blocks"))!;

    [Fact]
    public async Task Bundles_KeepTheirPresetsInOrder()
    {
        var first = Id(await AddPresetAsync());
        var second = Id(await AddPresetAsync());
        var response = await _client.PostAsJsonAsync("/api/building-blocks",
            new { label = " Favourites ", kind = "Bundle", presetIds = new[] { second, first } });
        response.EnsureSuccessStatusCode();
        var bundle = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bundle", bundle.GetProperty("kind").GetString());
        Assert.Equal("Favourites", bundle.GetProperty("label").GetString());
        Assert.Equal([second, first], PresetIds(bundle));

        (await _client.PutAsJsonAsync($"/api/building-blocks/{Id(bundle)}",
            new { label = "Favourites", kind = "Bundle", presetIds = new[] { first } })).EnsureSuccessStatusCode();

        Assert.Equal([first], PresetIds((await BlocksAsync()).Single(b => Id(b) == Id(bundle))));
    }

    [Fact]
    public async Task InvalidBundles_AreRejected()
    {
        var preset = Id(await AddPresetAsync());
        var text = Id(await AddBlockAsync("Light", "soft light"));
        object[] requests =
        [
            new { label = "", kind = "Bundle", presetIds = new[] { preset } },
            new { label = "Empty", kind = "Bundle", presetIds = Array.Empty<int>() },
            new { label = "Twice", kind = "Bundle", presetIds = new[] { preset, preset } },
            new { label = "Unknown", kind = "Bundle", presetIds = new[] { preset, 9999 } },
            new { label = "Text", kind = "Bundle", presetIds = new[] { text } },
        ];

        foreach (var request in requests)
            Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/building-blocks", request)).StatusCode);
    }

    [Fact]
    public async Task DeletingAPreset_RemovesItFromBundles()
    {
        var kept = Id(await AddPresetAsync());
        var deleted = Id(await AddPresetAsync());
        var bundle = await (await _client.PostAsJsonAsync("/api/building-blocks",
            new { label = "Both", kind = "Bundle", presetIds = new[] { deleted, kept } })).Content.ReadFromJsonAsync<JsonElement>();

        (await _client.DeleteAsync($"/api/building-blocks/{deleted}")).EnsureSuccessStatusCode();

        Assert.Equal([kept], PresetIds((await BlocksAsync()).Single(b => Id(b) == Id(bundle))));
    }

    [Fact]
    public async Task PresetImage_IsRejectedForTextBlocksAndUnsupportedTypes()
    {
        var text = await AddBlockAsync("Light", "soft light");
        var preset = await AddPresetAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await UploadImageAsync(text, [1], "image/png")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadImageAsync(preset, [1], "image/bmp")).StatusCode);
    }

    [Fact]
    public async Task BuildingBlocks_NeverExposeFilePaths()
    {
        var preset = await AddPresetAsync();
        (await UploadImageAsync(preset, [1, 2, 3], "image/png")).EnsureSuccessStatusCode();

        var body = await _client.GetStringAsync("/api/building-blocks");

        Assert.DoesNotContain("imagePath", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_factory.DataDirectory.Replace("\\", "\\\\"), body);
    }

    private async Task<JsonElement> AddBlockAsync(string label, string text)
    {
        var response = await _client.PostAsJsonAsync("/api/building-blocks", new { label, text });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
