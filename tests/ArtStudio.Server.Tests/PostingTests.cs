using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Tests.Fakes;
using static ArtStudio.Server.Tests.Fakes.StudioAppFactory;

namespace ArtStudio.Server.Tests;

public sealed class PostingTests : IDisposable
{
    private readonly StudioAppFactory _factory = new();
    private readonly HttpClient _client;

    public PostingTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private async Task<JsonElement[]> GalleriesAsync() =>
        (await _client.GetFromJsonAsync<JsonElement>("/api/deviantart/galleries")).EnumerateArray().ToArray();

    private static async Task<string?> ErrorOf(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
    }

    [Fact]
    public async Task Galleries_ListsEveryPageOfTheAccountsFolders()
    {
        await _factory.ConnectDeviantArtAsync();

        var galleries = await GalleriesAsync();

        Assert.Equal(["Featured", "Spicy", "RandomPile"], galleries.Select(g => g.GetProperty("name").GetString()));
        Assert.All(galleries, g => Assert.False(g.GetProperty("mature").GetBoolean()));
    }

    [Fact]
    public async Task MatureGalleries_AreRemembered()
    {
        await _factory.ConnectDeviantArtAsync();

        (await _client.PutAsJsonAsync("/api/deviantart/mature-galleries", new { folderIds = new[] { "F-SPICY" } }))
            .EnsureSuccessStatusCode();

        var mature = (await GalleriesAsync()).Where(g => g.GetProperty("mature").GetBoolean()).Select(g => g.GetProperty("name").GetString());
        Assert.Equal(["Spicy"], mature);
    }

    [Fact]
    public async Task Galleries_WithoutConnection_AskToConnect()
    {
        var error = await ErrorOf(await _client.GetAsync("/api/deviantart/galleries"));

        Assert.Contains("not connected", error);
    }

    private async Task<(int SetId, int[] PickedImageIds)> CreateReadySetAsync(int picks)
    {
        var setId = await _factory.CreateSetAsync(_client, "fox", count: picks);
        await WaitUntilAsync(async () => (await GetSetAsync(_client, setId)).Jobs.All(j => j.Status == JobStatus.Completed), "images");
        var picked = (await GetSetAsync(_client, setId)).Images.Select(i => i.Id).Reverse().ToArray();
        foreach (var imageId in picked)
            (await _client.PostAsJsonAsync($"/api/sets/{setId}/picks", new { imageId })).EnsureSuccessStatusCode();
        (await _client.PostAsync($"/api/sets/{setId}/ready", null)).EnsureSuccessStatusCode();
        return (setId, picked);
    }

    private Task<HttpResponseMessage> StashAsync(int setId, string title = "Ember Fox", string? description = "Inspired by a fox",
        string? tags = "digital_art made_with_ai") =>
        _client.PostAsJsonAsync($"/api/sets/{setId}/post/stash", new { title, description, tags });

    [Fact]
    public async Task Stash_UploadsPickOneWithTheDetails()
    {
        await _factory.ConnectDeviantArtAsync();
        var (setId, picked) = await CreateReadySetAsync(2);

        (await StashAsync(setId, " Ember Fox ", "Inspired by a fox", "digital_art  #made_with_ai digital_art")).EnsureSuccessStatusCode();

        var parts = Assert.Single(_factory.DeviantArt.StashSubmissions);
        Assert.Equal(
            ["title=Ember Fox", "artist_comments=Inspired by a fox", "tags[]=digital_art", "tags[]=made_with_ai",
                "is_ai_generated=true", "noai=false"],
            parts.Where(p => p.File is null).Select(p => $"{p.Name}={p.Value}"));
        var file = Assert.Single(parts, p => p.File is not null);
        Assert.Equal("file", file.Name);
        Assert.Equal("image/png", file.ContentType);
        Assert.Equal(await _client.GetByteArrayAsync($"/api/images/{picked[0]}"), file.File);

        var post = (await GetSetAsync(_client, setId)).Post;
        Assert.Equal(FakeDeviantArt.StashItemId.ToString(), post.StashItemId);
        Assert.Equal(("Ember Fox", "Inspired by a fox", "digital_art made_with_ai"), (post.Title, post.Description, post.Tags));
        Assert.NotNull(post.StashedAt);
        var summary = (await _client.GetFromJsonAsync<JsonElement>("/api/sets?stage=ready")).EnumerateArray().Single();
        Assert.True(summary.GetProperty("stashed").GetBoolean());
    }

    [Fact]
    public async Task Stash_WithoutDescription_SendsNoArtistComments()
    {
        await _factory.ConnectDeviantArtAsync();
        var (setId, _) = await CreateReadySetAsync(1);

        (await StashAsync(setId, description: "  ", tags: null)).EnsureSuccessStatusCode();

        Assert.Equal(["title", "is_ai_generated", "noai", "file"], _factory.DeviantArt.StashSubmissions.Single().Select(p => p.Name));
    }

    [Theory]
    [InlineData("", "digital_art", "Enter a title")]
    [InlineData("This title is far too long to fit DeviantArt's limit", "digital_art", "at most 50 characters")]
    [InlineData("Ember Fox", "made-with-ai", "'made-with-ai' may only contain")]
    public async Task Stash_WithInvalidDetails_IsRejected(string title, string tags, string expected)
    {
        await _factory.ConnectDeviantArtAsync();
        var (setId, _) = await CreateReadySetAsync(1);

        Assert.Contains(expected, await ErrorOf(await StashAsync(setId, title, tags: tags)));
        Assert.Empty(_factory.DeviantArt.StashSubmissions);
    }

    [Fact]
    public async Task Stash_OfASetNotInPost_IsRejected()
    {
        await _factory.ConnectDeviantArtAsync();
        var setId = await _factory.CreateSetAsync(_client, "fox");

        Assert.Contains("Only sets in Post", await ErrorOf(await StashAsync(setId)));
    }

    [Fact]
    public async Task StashedSet_CannotBeStashedAgain_MovedBackOrReordered_UntilForgotten()
    {
        await _factory.ConnectDeviantArtAsync();
        var (setId, picked) = await CreateReadySetAsync(2);
        (await StashAsync(setId)).EnsureSuccessStatusCode();

        Assert.Contains("already in your Stash", await ErrorOf(await StashAsync(setId)));
        Assert.Contains("Forget the Stash item", await ErrorOf(await _client.DeleteAsync($"/api/sets/{setId}/ready")));
        Assert.Contains("Forget the Stash item",
            await ErrorOf(await _client.PutAsJsonAsync($"/api/sets/{setId}/picks", new { imageIds = picked.Reverse() })));

        (await _client.PostAsync($"/api/sets/{setId}/post/forget-stash", null)).EnsureSuccessStatusCode();

        Assert.Null((await GetSetAsync(_client, setId)).Post.StashItemId);
        (await _client.DeleteAsync($"/api/sets/{setId}/ready")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ExtraPicks_AreCopiedInPickOrderAndTheFolderOpened()
    {
        var (setId, picked) = await CreateReadySetAsync(3);

        var response = await _client.PostAsync($"/api/sets/{setId}/post/extra-picks", null);

        response.EnsureSuccessStatusCode();
        var folder = Assert.Single(_factory.FolderOpener.Opened);
        Assert.Equal(["02.png", "03.png"], Directory.GetFiles(folder).Select(Path.GetFileName).Order());
        Assert.Equal(await _client.GetByteArrayAsync($"/api/images/{picked[1]}"), await File.ReadAllBytesAsync(Path.Combine(folder, "02.png")));
        Assert.Equal(await _client.GetByteArrayAsync($"/api/images/{picked[2]}"), await File.ReadAllBytesAsync(Path.Combine(folder, "03.png")));
    }

    [Fact]
    public async Task ExtraPicks_WithOnlyOnePick_IsRejected()
    {
        var (setId, _) = await CreateReadySetAsync(1);

        Assert.Contains("no picks besides #1", await ErrorOf(await _client.PostAsync($"/api/sets/{setId}/post/extra-picks", null)));
        Assert.Empty(_factory.FolderOpener.Opened);
    }

    [Fact]
    public async Task SuggestTitle_AsksTheVisionServerWithTheSetsPrompt()
    {
        await SetVisionApiKeyAsync(_client);
        var setId = await _factory.CreateSetAsync(_client, "A fox guarding a lantern in the snow.");
        _factory.Vision.Answer = "\"The Lantern Keeper\"\nSome commentary the model added.";

        var response = await _client.PostAsync($"/api/sets/{setId}/post/suggest-title", null);

        response.EnsureSuccessStatusCode();
        Assert.Equal("The Lantern Keeper", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
        var instruction = _factory.Vision.Requests.Last().Body["messages"]![0]!["content"]![0]!["text"]!.GetValue<string>();
        Assert.Contains("<prompt>\nA fox guarding a lantern in the snow.\n</prompt>", instruction.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task SuggestTitle_WithoutVisionKey_AsksForIt()
    {
        var setId = await _factory.CreateSetAsync(_client, "fox");

        Assert.Contains("vision API key", await ErrorOf(await _client.PostAsync($"/api/sets/{setId}/post/suggest-title", null)));
    }

    [Theory]
    [InlineData("Title: Ember Fox", "Ember Fox")]
    [InlineData("  \n**Ember Fox**\n", "Ember Fox")]
    [InlineData("A very long title about a fox that sits upon a mossy rock at dawn", "A very long title about a fox that sits upon a")]
    [InlineData("Averyveryveryveryveryveryveryveryveryverylongsingleword", "Averyveryveryveryveryveryveryveryveryverylongsingl")]
    public void TitleCleaning_KeepsOneLineOfAtMostFiftyCharacters(string answer, string expected) =>
        Assert.Equal(expected, ArtStudio.Server.Vision.TitleSuggester.Clean(answer));

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, """{ "error": "rate_limited" }""", "Try again in a minute")]
    [InlineData(HttpStatusCode.Forbidden, """{ "error": "insufficient_scope", "error_description": "scope" }""", "Reconnect DeviantArt")]
    [InlineData(HttpStatusCode.Unauthorized, """{ "error": "invalid_token" }""", "Reconnect it in Settings")]
    [InlineData(HttpStatusCode.BadRequest, """{ "error": "invalid_request", "error_description": "Gallery is broken." }""", "Gallery is broken.")]
    [InlineData(HttpStatusCode.BadGateway, "<html>oops</html>", "unreadable response (502)")]
    public async Task DeviantArtErrors_BecomeReadableMessages(HttpStatusCode status, string body, string expected)
    {
        await _factory.ConnectDeviantArtAsync();
        _factory.DeviantArt.ApiFailure = (status, body);

        var error = await ErrorOf(await _client.GetAsync("/api/deviantart/galleries"));

        Assert.Contains(expected, error);
    }
}
