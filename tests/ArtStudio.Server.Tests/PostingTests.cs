using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArtStudio.Server.Api;
using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    private async Task<JsonElement[]> SetsInStageAsync(string stage) =>
        (await _client.GetFromJsonAsync<JsonElement>($"/api/sets?stage={stage}")).EnumerateArray().ToArray();

    private Task<HttpResponseMessage> StashAsync(int setId, string title = "Ember Fox", string? description = "Inspired by a fox",
        string? tags = "digital_art made_with_ai") =>
        _client.PostAsJsonAsync($"/api/sets/{setId}/post/stash", new { title, description, tags });

    [Fact]
    public async Task Stash_UploadsPickOneWithTheDetails()
    {
        await _factory.ConnectDeviantArtAsync();
        var (setId, picked) = await CreateReadySetAsync(2);

        (await StashAsync(setId, " Ember Fox ", "A fox at <dawn> & dusk.\r\n\nInspired by\n", "digital_art  #made_with_ai digital_art")).EnsureSuccessStatusCode();

        var parts = Assert.Single(_factory.DeviantArt.StashSubmissions);
        Assert.Equal(
            ["title=Ember Fox", "artist_comments=A fox at &lt;dawn&gt; &amp; dusk.<br /><br />Inspired by<br />", "tags[]=digital_art", "tags[]=made_with_ai",
                "is_ai_generated=true", "noai=false"],
            parts.Where(p => p.File is null).Select(p => $"{p.Name}={p.Value}"));
        var file = Assert.Single(parts, p => p.File is not null);
        Assert.Equal("file", file.Name);
        Assert.Equal("image/png", file.ContentType);
        Assert.Equal(await _client.GetByteArrayAsync($"/api/images/{picked[0]}"), file.File);

        var post = (await GetSetAsync(_client, setId)).Post;
        Assert.Equal(FakeDeviantArt.StashItemId.ToString(), post.StashItemId);
        Assert.Equal(("Ember Fox", "A fox at <dawn> & dusk.\r\n\nInspired by\n", "digital_art made_with_ai"), (post.Title, post.Description, post.Tags));
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

    private const string InspirationUrl = "https://www.deviantart.com/someartist/art/Fox-123456789";

    private async Task InspireAsync(int setId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
        (await db.PromptSets.SingleAsync(s => s.Id == setId)).DeviantArtUrl = InspirationUrl;
        await db.SaveChangesAsync();
    }

    private string? DescriptionSent(List<StashPart> submission) =>
        submission.SingleOrDefault(p => p.Name == "artist_comments")?.Value;

    [Fact]
    public async Task Stash_WhenTheInspirationIsGone_LeavesOutInspiredBy()
    {
        await _factory.ConnectDeviantArtAsync();
        var (setId, _) = await CreateReadySetAsync(1);
        await InspireAsync(setId);
        _factory.DeviantArt.OEmbedFailure = HttpStatusCode.NotFound;

        (await StashAsync(setId, description: "A fox at dawn.\n\nInspired by\n")).EnsureSuccessStatusCode();

        Assert.Equal("A fox at dawn.", DescriptionSent(_factory.DeviantArt.StashSubmissions.Single()));
        Assert.Equal("A fox at dawn.", (await GetSetAsync(_client, setId)).Post.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Stash_WhenTheInspirationIsThereOrCannotBeChecked_KeepsInspiredBy(HttpStatusCode? oEmbed)
    {
        await _factory.ConnectDeviantArtAsync();
        var (setId, _) = await CreateReadySetAsync(1);
        await InspireAsync(setId);
        _factory.DeviantArt.OEmbedFailure = oEmbed;

        (await StashAsync(setId, description: "Inspired by\n")).EnsureSuccessStatusCode();

        Assert.Equal("Inspired by<br />", DescriptionSent(_factory.DeviantArt.StashSubmissions.Single()));
    }

    [Fact]
    public async Task Publish_WhenTheInspirationWentAwayAfterTheUpload_RemovesInspiredByFromTheStashItemFirst()
    {
        await _factory.ConnectDeviantArtAsync();
        var (setId, _) = await CreateReadySetAsync(1);
        await InspireAsync(setId);
        (await StashAsync(setId, description: "Inspired by\n", tags: "fox")).EnsureSuccessStatusCode();
        _factory.DeviantArt.OEmbedFailure = HttpStatusCode.NotFound;

        (await PublishAsync(setId)).EnsureSuccessStatusCode();

        var update = _factory.DeviantArt.StashSubmissions[1];
        Assert.Equal(
            [$"itemid={FakeDeviantArt.StashItemId}", "title=Ember Fox", "artist_comments=", "tags[]=fox", "is_ai_generated=true", "noai=false"],
            update.Select(p => $"{p.Name}={p.Value}"));
        Assert.Single(_factory.DeviantArt.Publications);
        Assert.Equal("", (await GetSetAsync(_client, setId)).Post.Description);
    }

    [Fact]
    public async Task Publish_WhenTheInspirationIsThere_LeavesTheStashItemAlone()
    {
        await _factory.ConnectDeviantArtAsync();
        var (setId, _) = await CreateReadySetAsync(1);
        await InspireAsync(setId);
        (await StashAsync(setId, description: "Inspired by\n")).EnsureSuccessStatusCode();

        (await PublishAsync(setId)).EnsureSuccessStatusCode();

        Assert.Single(_factory.DeviantArt.StashSubmissions);
        Assert.Single(_factory.DeviantArt.Publications);
    }

    private Task<HttpResponseMessage> PublishAsync(int setId, string galleryId = "F-RANDOM", bool isMature = false,
        string? matureLevel = null) =>
        _client.PostAsJsonAsync($"/api/sets/{setId}/post/publish", new { galleryId, isMature, matureLevel });

    [Fact]
    public async Task Publish_SendsTheOldAppsFields_AndMovesTheSetToPublished()
    {
        await _factory.ConnectDeviantArtAsync();
        var (setId, _) = await CreateReadySetAsync(2);
        (await StashAsync(setId, tags: "digital_art made_with_ai fox")).EnsureSuccessStatusCode();

        (await PublishAsync(setId, "F-RANDOM")).EnsureSuccessStatusCode();

        Assert.Equal(
            [$"itemid={FakeDeviantArt.StashItemId}", "is_mature=false", "galleryids[]=F-RANDOM", "feature=false",
                "allow_comments=true", "display_resolution=0", "license_options[creative_commons]=true",
                "license_options[commercial]=true", "license_options[modify]=share", "allow_free_download=true",
                "add_watermark=false", "tags[]=digital_art", "tags[]=made_with_ai", "tags[]=fox",
                "is_ai_generated=true", "noai=false"],
            Assert.Single(_factory.DeviantArt.Publications));

        var set = await GetSetAsync(_client, setId);
        Assert.Equal(FakeDeviantArt.PublishedUrl, set.Post.PublishedUrl);
        Assert.NotNull(set.Post.PublishedAt);
        Assert.Null(set.ArchivedAt);
        using (var scope = _factory.Services.CreateScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<StudioDbContext>().PromptSets.SingleAsync(s => s.Id == setId);
            Assert.Equal(FakeDeviantArt.PublishedDeviationId, stored.PublishedDeviationId);
        }
        var published = Assert.Single(await SetsInStageAsync("published"));
        Assert.Equal((setId, FakeDeviantArt.PublishedUrl), (published.GetProperty("id").GetInt32(), published.GetProperty("publishedUrl").GetString()));
        Assert.Equal(set.Post.PublishedAt, published.GetProperty("publishedAt").GetDateTimeOffset());
        Assert.Empty(await SetsInStageAsync("ready"));
        Assert.Empty(await SetsInStageAsync("archived"));
        var lastUsed = (await GalleriesAsync()).Where(g => g.GetProperty("lastUsed").GetBoolean()).Select(g => g.GetProperty("folderId").GetString());
        Assert.Equal(["F-RANDOM"], lastUsed);
    }

    [Fact]
    public async Task ArchivingAPublishedSet_AndRestoringReturnsItToPublished()
    {
        await _factory.ConnectDeviantArtAsync();
        var (setId, _) = await CreateReadySetAsync(2);
        (await StashAsync(setId)).EnsureSuccessStatusCode();
        (await PublishAsync(setId, "F-RANDOM")).EnsureSuccessStatusCode();

        (await _client.PostAsync($"/api/sets/{setId}/archive", null)).EnsureSuccessStatusCode();

        Assert.Empty(await SetsInStageAsync("published"));
        Assert.Single(await SetsInStageAsync("archived"));

        (await _client.DeleteAsync($"/api/sets/{setId}/archive")).EnsureSuccessStatusCode();

        Assert.Single(await SetsInStageAsync("published"));
        Assert.Empty(await SetsInStageAsync("archived"));
    }

    [Fact]
    public async Task Publish_AsMature_SendsTheLevel()
    {
        await _factory.ConnectDeviantArtAsync();
        var (setId, _) = await CreateReadySetAsync(1);
        (await StashAsync(setId)).EnsureSuccessStatusCode();

        (await PublishAsync(setId, "F-SPICY", isMature: true, matureLevel: "strict")).EnsureSuccessStatusCode();

        Assert.Equal(["itemid", "is_mature=true", "mature_level=strict", "galleryids[]=F-SPICY"],
            _factory.DeviantArt.Publications.Single().Take(4).Select(f => f.StartsWith("itemid=") ? "itemid" : f));
    }

    [Fact]
    public async Task Publish_IsRejected_WhenNotStashed_MatureWithoutLevel_WithoutGallery_OrAlreadyPublished()
    {
        await _factory.ConnectDeviantArtAsync();
        var (setId, _) = await CreateReadySetAsync(1);

        Assert.Contains("Upload the set to your Stash first", await ErrorOf(await PublishAsync(setId)));
        (await StashAsync(setId)).EnsureSuccessStatusCode();
        Assert.Contains("moderate or strict", await ErrorOf(await PublishAsync(setId, isMature: true)));
        Assert.Contains("Choose a gallery", await ErrorOf(await PublishAsync(setId, galleryId: " ")));
        Assert.Empty(_factory.DeviantArt.Publications);

        (await PublishAsync(setId)).EnsureSuccessStatusCode();
        (await _client.DeleteAsync($"/api/sets/{setId}/archive")).EnsureSuccessStatusCode();

        Assert.Contains("already published", await ErrorOf(await PublishAsync(setId)));
        Assert.Single(_factory.DeviantArt.Publications);
    }

    [Fact]
    public async Task FakePublishHandler_AnswersPublishItself_AndPassesEverythingElseOn()
    {
        var network = new FakeHttpHandler(_ => FakeHttpHandler.Json("""{ "status": "success", "itemid": 1 }"""));
        using var http = new HttpClient(new ArtStudio.Server.DeviantArt.FakeDeviantArtPublishHandler { InnerHandler = network });

        var published = await http.PostAsync("https://www.deviantart.com/api/v1/oauth2/stash/publish", new StringContent("itemid=1"));
        await http.PostAsync("https://www.deviantart.com/api/v1/oauth2/stash/submit", new StringContent("title=x"));

        Assert.Contains("artstudio-fake", (await published.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString());
        Assert.Equal(["/api/v1/oauth2/stash/submit"], network.Requests.Select(r => r.PathAndQuery));
    }

    [Fact]
    public async Task PublishChoice_IsKeptOnTheSet_UntilChangedAgain()
    {
        var (setId, _) = await CreateReadySetAsync(1);
        Assert.Null((await GetSetAsync(_client, setId)).Post.PublishGalleryId);

        (await _client.PutAsJsonAsync($"/api/sets/{setId}/post/publish-choice", new { galleryId = "F-SPICY", matureLevel = "strict" }))
            .EnsureSuccessStatusCode();
        var chosen = (await GetSetAsync(_client, setId)).Post;
        (await _client.PutAsJsonAsync($"/api/sets/{setId}/post/publish-choice", new { galleryId = "F-SCENERY", matureLevel = "none" }))
            .EnsureSuccessStatusCode();
        var changed = (await GetSetAsync(_client, setId)).Post;

        Assert.Equal(("F-SPICY", "strict"), (chosen.PublishGalleryId, chosen.PublishMatureLevel));
        Assert.Equal(("F-SCENERY", "none"), (changed.PublishGalleryId, changed.PublishMatureLevel));
    }

    [Fact]
    public async Task PublishChoice_WithAnUnknownMatureLevel_IsRefused()
    {
        var (setId, _) = await CreateReadySetAsync(1);

        var response = await _client.PutAsJsonAsync($"/api/sets/{setId}/post/publish-choice", new { galleryId = "F-SPICY", matureLevel = "extreme" });

        Assert.Contains("how mature", await ErrorOf(response));
        Assert.Null((await GetSetAsync(_client, setId)).Post.PublishMatureLevel);
    }

    [Fact]
    public async Task StashExtraPicks_ListPicksTwoAndUpInPickOrder_ByTheStashItem()
    {
        await _factory.ConnectDeviantArtAsync();
        var (setId, picked) = await CreateReadySetAsync(3);
        (await StashAsync(setId)).EnsureSuccessStatusCode();
        var stashItemId = (await GetSetAsync(_client, setId)).Post.StashItemId;

        var extra = await _client.GetFromJsonAsync<StashExtraPicksDto>($"/api/stash/{stashItemId}/extra-picks", Json);

        Assert.Equal(setId, extra!.SetId);
        Assert.Equal(
            [new ExtraPickDto(2, "02.png", $"/api/images/{picked[1]}"), new ExtraPickDto(3, "03.png", $"/api/images/{picked[2]}")],
            extra.Picks);
    }

    [Fact]
    public async Task StashExtraPicks_AreEmptyForOnePick_AndNotFoundForAnUnknownItem()
    {
        await _factory.ConnectDeviantArtAsync();
        var (setId, _) = await CreateReadySetAsync(1);
        (await StashAsync(setId)).EnsureSuccessStatusCode();
        var stashItemId = (await GetSetAsync(_client, setId)).Post.StashItemId;

        var extra = await _client.GetFromJsonAsync<StashExtraPicksDto>($"/api/stash/{stashItemId}/extra-picks", Json);
        var unknown = await _client.GetAsync("/api/stash/987654321/extra-picks");

        Assert.Empty(extra!.Picks);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
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
        Assert.False(_factory.Vision.Requests.Last().Body["chat_template_kwargs"]!["enable_thinking"]!.GetValue<bool>());
    }

    private async Task<string[]> SuggestTagsAsync(int setId)
    {
        var response = await _client.PostAsync($"/api/sets/{setId}/post/suggest-tags", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tags").EnumerateArray()
            .Select(tag => tag.GetString()!).ToArray();
    }

    [Fact]
    public async Task SuggestTags_KeepsOnlyTagsDeviantArtKnows_InTheModelsOrder()
    {
        await SetVisionApiKeyAsync(_client);
        await _factory.ConnectDeviantArtAsync();
        var setId = await _factory.CreateSetAsync(_client, "A fox guarding a lantern in the snow.");
        _factory.DeviantArt.KnownTags.UnionWith(["fox", "lantern", "winter_night", "snow", "digital_art"]);
        _factory.Vision.Answer = "1. #Fox\n2. \"lantern\"\n- **Winter night**\nnotarealtag, snow, fox\ndigital_art\nab";

        Assert.Equal(["fox", "lantern", "winter_night", "snow"], await SuggestTagsAsync(setId));
        Assert.False(_factory.Vision.Requests.Last().Body["chat_template_kwargs"]!["enable_thinking"]!.GetValue<bool>());
        Assert.Contains("/api/v1/oauth2/browse/tags?tag=notarealtag&limit=1&mature_content=true", _factory.DeviantArt.TagChecks);
    }

    [Fact]
    public async Task SuggestTags_ReturnsAtMostTenTags()
    {
        await SetVisionApiKeyAsync(_client);
        await _factory.ConnectDeviantArtAsync();
        var setId = await _factory.CreateSetAsync(_client, "fox");
        var tags = Enumerable.Range(1, 15).Select(n => $"tag{n:00}").ToArray();
        _factory.DeviantArt.KnownTags.UnionWith(tags);
        _factory.Vision.Answer = string.Join('\n', tags);

        Assert.Equal(tags.Take(10), await SuggestTagsAsync(setId));
    }

    [Fact]
    public async Task SuggestTags_WithoutDeviantArtLogin_SaysSo()
    {
        await SetVisionApiKeyAsync(_client);
        var setId = await _factory.CreateSetAsync(_client, "fox");
        _factory.Vision.Answer = "fox";

        Assert.Contains("DeviantArt", await ErrorOf(await _client.PostAsync($"/api/sets/{setId}/post/suggest-tags", null)));
    }

    private async Task<int> CreateSetReadyToMarkAsync(string? title = null, string? tags = null)
    {
        var setId = await _factory.CreateSetAsync(_client, "fox");
        await WaitUntilAsync(async () => (await GetSetAsync(_client, setId)).Jobs.All(j => j.Status == JobStatus.Completed), "images");
        var imageId = (await GetSetAsync(_client, setId)).Images.First().Id;
        (await _client.PostAsJsonAsync($"/api/sets/{setId}/picks", new { imageId })).EnsureSuccessStatusCode();
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<StudioDbContext>().PromptSets.Where(s => s.Id == setId)
            .ExecuteUpdateAsync(s => s.SetProperty(set => set.PostTitle, title).SetProperty(set => set.PostTags, tags));
        return setId;
    }

    private async Task<PostDto> PostAfterSuggestionsAsync(int setId)
    {
        (await _client.PostAsync($"/api/sets/{setId}/ready", null)).EnsureSuccessStatusCode();
        await WaitUntilAsync(async () => !(await GetSetAsync(_client, setId)).Post.Suggesting, "suggestions");
        return (await GetSetAsync(_client, setId)).Post;
    }

    private async Task PrepareSuggestionsAsync()
    {
        await SetVisionApiKeyAsync(_client);
        await _factory.ConnectDeviantArtAsync();
        _factory.DeviantArt.KnownTags.Add("ember_fox");
        _factory.Vision.Answer = "Ember Fox";
    }

    [Fact]
    public async Task MarkingReady_SuggestsTheMissingTitleAndTags()
    {
        await PrepareSuggestionsAsync();
        var setId = await CreateSetReadyToMarkAsync(tags: "digital_art #made_with_ai");

        var post = await PostAfterSuggestionsAsync(setId);

        Assert.Equal(("Ember Fox", "digital_art made_with_ai ember_fox"), (post.Title, post.Tags));
    }

    [Fact]
    public async Task MarkingReady_WhenDeviantArtIsLimiting_RetriesTheTags()
    {
        await PrepareSuggestionsAsync();
        _factory.DeviantArt.RateLimitedTagChecks = 2;
        var setId = await CreateSetReadyToMarkAsync();

        var post = await PostAfterSuggestionsAsync(setId);

        Assert.Equal(("Ember Fox", "digital_art made_with_ai ember_fox"), (post.Title, post.Tags));
        Assert.Equal(0, _factory.DeviantArt.RateLimitedTagChecks);
    }

    [Fact]
    public async Task ClickedSuggestion_WaitsForTheRunningBackgroundOne_ThenGoesBeforeItsNextStep()
    {
        await PrepareSuggestionsAsync();
        var backgroundSet = await CreateSetReadyToMarkAsync();
        var clickedSet = await _factory.CreateSetAsync(_client, "A heron at dawn.");
        _factory.Vision.HoldAnswers = true;
        (await _client.PostAsync($"/api/sets/{backgroundSet}/ready", null)).EnsureSuccessStatusCode();
        await WaitUntilAsync(() => Task.FromResult(_factory.Vision.Requests.Count == 1), "the background title request");

        var click = _client.PostAsync($"/api/sets/{clickedSet}/post/suggest-title", null);
        await Task.Delay(200);
        Assert.False(click.IsCompleted);
        Assert.Single(_factory.Vision.Requests);
        _factory.Vision.HoldAnswers = false;

        (await click).EnsureSuccessStatusCode();
        Assert.Contains("A heron at dawn.", _factory.Vision.Requests[1].Body.ToJsonString());
        await WaitUntilAsync(async () => !(await GetSetAsync(_client, backgroundSet)).Post.Suggesting, "suggestions");
        var post = (await GetSetAsync(_client, backgroundSet)).Post;
        Assert.Equal(("Ember Fox", "digital_art made_with_ai ember_fox"), (post.Title, post.Tags));
    }

    [Fact]
    public async Task MarkingReady_KeepsAnExistingTitle()
    {
        await PrepareSuggestionsAsync();
        var setId = await CreateSetReadyToMarkAsync(title: "My Own Title");

        var post = await PostAfterSuggestionsAsync(setId);

        Assert.Equal(("My Own Title", "digital_art made_with_ai ember_fox"), (post.Title, post.Tags));
    }

    [Fact]
    public async Task MarkingReady_KeepsExistingTags_AndOnlySuggestsTheTitle()
    {
        await PrepareSuggestionsAsync();
        var setId = await CreateSetReadyToMarkAsync(tags: "digital_art fox");

        var post = await PostAfterSuggestionsAsync(setId);

        Assert.Equal(("Ember Fox", "digital_art fox"), (post.Title, post.Tags));
        Assert.Empty(_factory.DeviantArt.TagChecks);
    }

    [Fact]
    public async Task MarkingReady_WithoutVisionKey_StillMovesTheSet_WithoutSuggestions()
    {
        var setId = await CreateSetReadyToMarkAsync();

        var post = await PostAfterSuggestionsAsync(setId);

        Assert.NotNull((await GetSetAsync(_client, setId)).ReadyToPostAt);
        Assert.Equal((null, null), (post.Title, post.Tags));
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("Ember Fox", "digital_art made_with_ai", false)]
    [InlineData(null, "digital_art fox", false)]
    [InlineData("Ember Fox", "digital_art fox", true)]
    public async Task ReadyList_TellsWhetherASetHasATitleAndTags(string? title, string? tags, bool expected)
    {
        var setId = await CreateSetReadyToMarkAsync(title, tags);
        (await _client.PostAsync($"/api/sets/{setId}/ready", null)).EnsureSuccessStatusCode();

        var summary = (await _client.GetFromJsonAsync<JsonElement>("/api/sets?stage=ready")).EnumerateArray().Single();

        Assert.Equal(expected, summary.GetProperty("hasTitleAndTags").GetBoolean());
    }

    [Fact]
    public async Task SuggestTags_WithoutVisionKey_AsksForIt()
    {
        var setId = await _factory.CreateSetAsync(_client, "fox");

        Assert.Contains("vision API key", await ErrorOf(await _client.PostAsync($"/api/sets/{setId}/post/suggest-tags", null)));
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
