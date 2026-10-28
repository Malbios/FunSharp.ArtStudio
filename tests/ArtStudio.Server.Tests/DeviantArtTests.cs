using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ArtStudio.Server.Tests.Fakes.StudioAppFactory;

namespace ArtStudio.Server.Tests;

public sealed class DeviantArtTests : IDisposable
{
    private const string DeviationLink = "https://www.deviantart.com/someartist/art/Misty-Forest-123456";

    private readonly StudioAppFactory _factory = new();
    private readonly HttpClient _client;

    public DeviantArtTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private Task<HttpResponseMessage> PostSetFromDeviationAsync(string url, int? imageIndex = null)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("forest"), "prompt" },
            { new StringContent("Wide"), "resolution" },
            { new StringContent("1"), "count" },
            { new StringContent("DeviantArt"), "sourceKind" },
            { new StringContent(url), "deviantArtUrl" },
        };
        if (imageIndex is not null)
            form.Add(new StringContent(imageIndex.Value.ToString()), "deviantArtImageIndex");
        return _client.PostAsync("/api/sets", form);
    }

    [Fact]
    public async Task Preview_MultiImagePost_ListsMainAndExtraImages()
    {
        _factory.DeviantArt.ExtraImages = 3;

        var preview = await ReadJsonAsync(await _client.PostAsJsonAsync("/api/deviantart/preview", new { url = DeviationLink }));

        var images = preview.GetProperty("images").EnumerateArray().ToList();
        Assert.Equal([0, 1, 2, 3], images.Select(i => i.GetProperty("index").GetInt32()));
        Assert.Equal(FakeDeviantArt.ImageUrl, images[0].GetProperty("imageUrl").GetString());
        Assert.Equal(DeviationPageFixture.ExtraImageUrl(2), images[2].GetProperty("imageUrl").GetString());
        Assert.Equal(0, preview.GetProperty("unavailableImageCount").GetInt32());
    }

    [Fact]
    public async Task CreateSet_WithChosenImage_StoresThatImage()
    {
        _factory.DeviantArt.ExtraImages = 3;

        var response = await PostSetFromDeviationAsync(DeviationLink, imageIndex: 2);

        response.EnsureSuccessStatusCode();
        var set = await GetSetAsync(_client, (await ReadJsonAsync(response)).GetProperty("id").GetInt32());
        Assert.Equal(DeviationPageFixture.ExtraImageBytes(2), await _client.GetByteArrayAsync(set.SourceImageUrl));
        Assert.Equal(DeviationLink, set.DeviantArtUrl);
    }

    [Fact]
    public async Task CreateSet_WithMissingImageIndex_IsRejected()
    {
        _factory.DeviantArt.ExtraImages = 1;

        var response = await PostSetFromDeviationAsync(DeviationLink, imageIndex: 5);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("#6", (await ReadJsonAsync(response)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task MaturePost_HidesBlurredExtraImages()
    {
        _factory.DeviantArt.Mature = true;
        _factory.DeviantArt.ExtraImages = 2;
        await _factory.ConnectDeviantArtAsync();

        var preview = await ReadJsonAsync(await _client.PostAsJsonAsync("/api/deviantart/preview", new { url = DeviationLink }));

        var image = Assert.Single(preview.GetProperty("images").EnumerateArray());
        Assert.Equal(FakeDeviantArt.ApiImageUrl, image.GetProperty("imageUrl").GetString());
        Assert.Equal(2, preview.GetProperty("unavailableImageCount").GetInt32());
    }

    [Fact]
    public async Task SamePost_WithDifferentImage_IsStillADuplicate()
    {
        _factory.DeviantArt.ExtraImages = 2;
        (await PostSetFromDeviationAsync(DeviationLink, imageIndex: 1)).EnsureSuccessStatusCode();

        var again = await PostSetFromDeviationAsync(DeviationLink, imageIndex: 2);

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
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
        Assert.Equal(1600, preview.GetProperty("images")[0].GetProperty("width").GetInt32());
        Assert.Equal(900, preview.GetProperty("images")[0].GetProperty("height").GetInt32());
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
    public async Task DeletedSet_FreesItsDeviation()
    {
        var first = await PostSetFromDeviationAsync(DeviationLink);
        var firstId = (await ReadJsonAsync(first)).GetProperty("id").GetInt32();

        (await _client.DeleteAsync($"/api/sets/{firstId}")).EnsureSuccessStatusCode();
        var again = await PostSetFromDeviationAsync(DeviationLink);

        again.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task RequeueWithNewPrompt_KeepsDeviationOnSameSetAndBlocked()
    {
        var first = await PostSetFromDeviationAsync(DeviationLink);
        var setId = (await ReadJsonAsync(first)).GetProperty("id").GetInt32();

        (await _client.PostAsJsonAsync($"/api/sets/{setId}/more", new { count = 1, prompt = "forest, oil painting" }))
            .EnsureSuccessStatusCode();
        var again = await PostSetFromDeviationAsync(DeviationLink);

        Assert.Equal(DeviationLink, (await GetSetAsync(_client, setId)).DeviantArtUrl);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal(setId, (await ReadJsonAsync(again)).GetProperty("existingSetId").GetInt32());
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

        Assert.Equal(FakeDeviantArt.ApiImageUrl, preview.GetProperty("images")[0].GetProperty("imageUrl").GetString());
        Assert.Equal(3000, preview.GetProperty("images")[0].GetProperty("width").GetInt32());
        Assert.Equal(2000, preview.GetProperty("images")[0].GetProperty("height").GetInt32());
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

    private async Task<int> SeedSetAsync(string? author, Action<PromptSet>? stage = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
        var set = new PromptSet { Prompt = "forest", Resolution = "Wide", DeviantArtAuthor = author, CreatedAt = DateTimeOffset.UtcNow };
        stage?.Invoke(set);
        db.PromptSets.Add(set);
        await db.SaveChangesAsync();
        return set.Id;
    }

    private async Task<PromptSet?> StoredSetAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<StudioDbContext>().PromptSets.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
    }

    [Fact]
    public async Task BlockingAnArtist_ArchivesTheirSets_AndDeletesTheirDrafts()
    {
        const string postedUrl = "https://www.deviantart.com/me/art/Posted-1";
        var working = await SeedSetAsync("SomeArtist");
        var ready = await SeedSetAsync("someartist", s => s.ReadyToPostAt = DateTimeOffset.UtcNow);
        var published = await SeedSetAsync("SOMEARTIST", s =>
        {
            s.ReadyToPostAt = s.PublishedAt = DateTimeOffset.UtcNow;
            s.PublishedUrl = postedUrl;
        });
        var draft = await SeedSetAsync("someartist", s => s.IsDraft = true);
        var otherArtist = await SeedSetAsync("otherartist");
        var ownImage = await SeedSetAsync(author: null);

        var result = await ReadJsonAsync(await _client.PostAsJsonAsync("/api/blocked-artists", new { username = "someartist" }));

        Assert.Equal(3, result.GetProperty("archivedSets").GetInt32());
        Assert.Equal(1, result.GetProperty("deletedDrafts").GetInt32());
        Assert.Equal([postedUrl], result.GetProperty("postedUrls").EnumerateArray().Select(u => u.GetString()));
        foreach (var id in new[] { working, ready, published })
            Assert.NotNull((await StoredSetAsync(id))!.ArchivedAt);
        Assert.Null(await StoredSetAsync(draft));
        Assert.Null((await StoredSetAsync(otherArtist))!.ArchivedAt);
        Assert.Null((await StoredSetAsync(ownImage))!.ArchivedAt);
    }

    [Fact]
    public async Task BlockingAnArtist_CancelsTheQueuedJobsOfTheirSets()
    {
        (await _client.PostAsync("/api/queue/pause", null)).EnsureSuccessStatusCode();
        var set = await ReadJsonAsync(await PostSetFromDeviationAsync(DeviationLink));
        var setId = set.GetProperty("id").GetInt32();

        (await _client.PostAsJsonAsync("/api/blocked-artists", new { username = _factory.DeviantArt.Author })).EnsureSuccessStatusCode();

        var detail = await GetSetAsync(_client, setId);
        Assert.NotNull(detail.ArchivedAt);
        Assert.Equal(JobStatus.Cancelled, detail.Jobs.Single().Status);
    }

    [Fact]
    public async Task BlockArtist_AcceptsProfileUrl_AndRejectsDuplicates()
    {
        var added = await _client.PostAsJsonAsync("/api/blocked-artists", new { username = "https://www.deviantart.com/someartist" });
        added.EnsureSuccessStatusCode();
        var artist = (await ReadJsonAsync(added)).GetProperty("artist");
        Assert.Equal("someartist", artist.GetProperty("username").GetString());

        var duplicate = await _client.PostAsJsonAsync("/api/blocked-artists", new { username = "SOMEARTIST" });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        var id = artist.GetProperty("id").GetInt32();
        (await _client.DeleteAsync($"/api/blocked-artists/{id}")).EnsureSuccessStatusCode();
        Assert.Equal(0, (await _client.GetFromJsonAsync<JsonElement>("/api/blocked-artists")).GetArrayLength());
    }
}
