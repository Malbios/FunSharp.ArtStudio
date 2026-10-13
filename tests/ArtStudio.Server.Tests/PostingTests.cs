using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
