using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Tests.Fakes;
using static ArtStudio.Server.Tests.Fakes.StudioAppFactory;

namespace ArtStudio.Server.Tests;

public sealed class DeviantArtTests : IDisposable
{
    private const string DeviationLink = "https://www.deviantart.com/someartist/art/Misty-Forest-123456";

    private readonly StudioAppFactory _factory = new();
    private readonly HttpClient _client;

    public DeviantArtTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private Task<HttpResponseMessage> PostSetFromDeviationAsync(string url)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("forest"), "prompt" },
            { new StringContent("Wide"), "resolution" },
            { new StringContent("1"), "count" },
            { new StringContent("DeviantArt"), "sourceKind" },
            { new StringContent(url), "deviantArtUrl" },
        };
        return _client.PostAsync("/api/sets", form);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Preview_ReturnsImageAndDimensions()
    {
        var response = await _client.PostAsJsonAsync("/api/deviantart/preview", new { url = DeviationLink });

        response.EnsureSuccessStatusCode();
        var preview = await ReadJsonAsync(response);
        Assert.Equal("someartist", preview.GetProperty("author").GetString());
        Assert.Equal(1600, preview.GetProperty("width").GetInt32());
        Assert.Equal(900, preview.GetProperty("height").GetInt32());
        Assert.Equal("123456", preview.GetProperty("deviationId").GetString());
    }

    [Fact]
    public async Task CreateSet_FromDeviation_StoresImageAndInspiration()
    {
        var response = await PostSetFromDeviationAsync(DeviationLink);

        response.EnsureSuccessStatusCode();
        var setId = (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
        var set = await GetSetAsync(_client, setId);
        Assert.Equal(SourceKind.DeviantArt, set.SourceKind);
        Assert.Equal(DeviationLink, set.DeviantArtUrl);
        Assert.Equal("someartist", set.DeviantArtAuthor);
        Assert.Equal(FakeDeviantArt.JpegBytes, await _client.GetByteArrayAsync(set.SourceImageUrl));
    }

    [Fact]
    public async Task CreateSet_SameDeviationTwice_IsBlockedWithHint()
    {
        var first = await PostSetFromDeviationAsync(DeviationLink);
        var firstId = (await ReadJsonAsync(first)).GetProperty("id").GetInt32();

        var second = await PostSetFromDeviationAsync("https://www.deviantart.com/someartist/art/Renamed-Title-123456?ref=x");

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var error = await ReadJsonAsync(second);
        Assert.Equal(firstId, error.GetProperty("existingSetId").GetInt32());
        Assert.Contains($"#{firstId}", error.GetProperty("error").GetString());
    }

    [Fact]
    public async Task EditAsNewSet_FromDeviationSet_IsNotTreatedAsDuplicate()
    {
        var first = await PostSetFromDeviationAsync(DeviationLink);
        var firstId = (await ReadJsonAsync(first)).GetProperty("id").GetInt32();

        var copyId = await _factory.CreateSetAsync(_client, "forest, edited", count: 1,
            addSource: form => form.Add(new StringContent(firstId.ToString()), "basedOnSetId"));

        Assert.Equal(DeviationLink, (await GetSetAsync(_client, copyId)).DeviantArtUrl);
    }

    [Fact]
    public async Task MatureDeviation_WithoutLogin_IsRejectedWithConnectHint()
    {
        _factory.DeviantArt.Mature = true;

        var response = await _client.PostAsJsonAsync("/api/deviantart/preview", new { url = DeviationLink });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Connect DeviantArt", (await ReadJsonAsync(response)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task MatureDeviation_WhenConnected_UsesApiImage()
    {
        _factory.DeviantArt.Mature = true;
        await _factory.ConnectDeviantArtAsync();

        var response = await PostSetFromDeviationAsync(DeviationLink);

        response.EnsureSuccessStatusCode();
        var set = await GetSetAsync(_client, (await ReadJsonAsync(response)).GetProperty("id").GetInt32());
        Assert.Equal(FakeDeviantArt.ApiJpegBytes, await _client.GetByteArrayAsync(set.SourceImageUrl));
        Assert.Equal("someartist", set.DeviantArtAuthor);
        Assert.Equal(0, _factory.DeviantArt.OEmbedCalls);
        Assert.NotEmpty(_factory.DeviantArt.DeviationApiRequests);
        Assert.All(_factory.DeviantArt.DeviationApiRequests, call =>
        {
            Assert.StartsWith("Bearer access-1 ", call);
            Assert.Contains($"/deviation/{FakeDeviantArt.DeviationUuid}?mature_content=true", call);
        });
    }

    [Fact]
    public async Task Preview_WhenConnected_ReturnsApiDimensions()
    {
        await _factory.ConnectDeviantArtAsync();

        var preview = await ReadJsonAsync(await _client.PostAsJsonAsync("/api/deviantart/preview", new { url = DeviationLink }));

        Assert.Equal(FakeDeviantArt.ApiImageUrl, preview.GetProperty("imageUrl").GetString());
        Assert.Equal(3000, preview.GetProperty("width").GetInt32());
        Assert.Equal(2000, preview.GetProperty("height").GetInt32());
    }

    [Fact]
    public async Task BlockedAuthorFromApi_IsRejected()
    {
        await _factory.ConnectDeviantArtAsync();
        _factory.DeviantArt.Author = "HiddenAuthor";
        (await _client.PostAsJsonAsync("/api/blocked-artists", new { username = "hiddenauthor" })).EnsureSuccessStatusCode();

        var response = await _client.PostAsJsonAsync("/api/deviantart/preview", new { url = DeviationLink });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BlockedArtistFromUrl_IsRejectedBeforeFetching()
    {
        (await _client.PostAsJsonAsync("/api/blocked-artists", new { username = "SomeArtist" })).EnsureSuccessStatusCode();

        var response = await PostSetFromDeviationAsync(DeviationLink);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("block list", (await ReadJsonAsync(response)).GetProperty("error").GetString());
        Assert.Equal(0, _factory.DeviantArt.OEmbedCalls);
    }

    [Fact]
    public async Task BlockedArtistFromOEmbedAuthor_IsRejected()
    {
        (await _client.PostAsJsonAsync("/api/blocked-artists", new { username = "hiddenauthor" })).EnsureSuccessStatusCode();
        _factory.DeviantArt.Author = "HiddenAuthor";

        var response = await _client.PostAsJsonAsync("/api/deviantart/preview", new { url = "https://www.deviantart.com/deviation/777" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BlockArtist_AcceptsProfileUrl_AndRejectsDuplicates()
    {
        var added = await _client.PostAsJsonAsync("/api/blocked-artists", new { username = "https://www.deviantart.com/someartist" });
        added.EnsureSuccessStatusCode();
        var artist = await ReadJsonAsync(added);
        Assert.Equal("someartist", artist.GetProperty("username").GetString());

        var duplicate = await _client.PostAsJsonAsync("/api/blocked-artists", new { username = "SOMEARTIST" });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        var id = artist.GetProperty("id").GetInt32();
        (await _client.DeleteAsync($"/api/blocked-artists/{id}")).EnsureSuccessStatusCode();
        Assert.Equal(0, (await _client.GetFromJsonAsync<JsonElement>("/api/blocked-artists")).GetArrayLength());
    }
}
