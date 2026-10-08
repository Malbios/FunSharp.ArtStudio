using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Tests.Fakes;
using static ArtStudio.Server.Tests.Fakes.StudioAppFactory;

namespace ArtStudio.Server.Tests;

public sealed class DraftTests : IDisposable
{
    private const string DeviationLink = "https://www.deviantart.com/someartist/art/Misty-Forest-123456";

    private readonly StudioAppFactory _factory = new();
    private readonly HttpClient _client;

    public DraftTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private Task<HttpResponseMessage> PostDraftAsync(Action<MultipartFormDataContent> addSource)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(""), "prompt" },
            { new StringContent("Wide"), "resolution" },
            { new StringContent("2"), "count" },
            { new StringContent("true"), "draft" },
        };
        addSource(form);
        return _client.PostAsync("/api/sets", form);
    }

    private static void AddUpload(MultipartFormDataContent form)
    {
        form.Add(new StringContent("Upload"), "sourceKind");
        var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "image", "fox.png");
    }

    private static void AddDeviation(MultipartFormDataContent form)
    {
        form.Add(new StringContent("DeviantArt"), "sourceKind");
        form.Add(new StringContent(DeviationLink), "deviantArtUrl");
    }

    private async Task<int> CreateDraftAsync(Action<MultipartFormDataContent> addSource)
    {
        var response = await PostDraftAsync(addSource);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private Task<HttpResponseMessage> QueueDraftAsync(int setId, string prompt) =>
        _client.PostAsJsonAsync($"/api/sets/{setId}/queue", new { prompt, resolution = "Portrait", count = 3 });

    private async Task<int[]> SetIdsAsync(string stage) =>
        (await _client.GetFromJsonAsync<JsonElement>($"/api/sets?stage={stage}"))
            .EnumerateArray().Select(s => s.GetProperty("id").GetInt32()).ToArray();

    [Fact]
    public async Task Draft_WithoutPrompt_IsNotQueued()
    {
        var setId = await CreateDraftAsync(AddUpload);

        var set = await GetSetAsync(_client, setId);
        Assert.True(set.IsDraft);
        Assert.Empty(set.Jobs);
        Assert.Empty((await GetQueueAsync(_client)).Active);
        Assert.Equal([setId], await SetIdsAsync("draft"));
        Assert.Empty(await SetIdsAsync("working"));
    }

    [Fact]
    public async Task Drafts_AreListedOldestFirst()
    {
        var first = await CreateDraftAsync(AddUpload);
        var second = await CreateDraftAsync(AddUpload);

        Assert.Equal([first, second], await SetIdsAsync("draft"));
    }

    [Fact]
    public async Task Draft_WithoutImage_IsRejected()
    {
        var response = await PostDraftAsync(_ => { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await SetIdsAsync("draft"));
    }

    [Fact]
    public async Task QueueDraft_WithoutPrompt_IsRejected_AndStaysDraft()
    {
        var setId = await CreateDraftAsync(AddUpload);

        var response = await QueueDraftAsync(setId, "  ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await GetSetAsync(_client, setId)).IsDraft);
    }

    [Fact]
    public async Task QueueDraft_QueuesJob_AndMovesSetToWorking()
    {
        var setId = await CreateDraftAsync(AddUpload);

        (await QueueDraftAsync(setId, "a fox")).EnsureSuccessStatusCode();

        var set = await GetSetAsync(_client, setId);
        Assert.False(set.IsDraft);
        var job = Assert.Single(set.Jobs);
        Assert.Equal("a fox", job.Prompt);
        Assert.Equal("Portrait", job.Resolution);
        Assert.Equal(3, job.RequestedCount);
        Assert.Empty(await SetIdsAsync("draft"));
        Assert.Equal([setId], await SetIdsAsync("working"));
    }

    [Fact]
    public async Task QueueDraft_OnQueuedSet_IsRejected()
    {
        var setId = await CreateDraftAsync(AddUpload);
        (await QueueDraftAsync(setId, "a fox")).EnsureSuccessStatusCode();

        var again = await QueueDraftAsync(setId, "a fox");

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task MoreImages_OnDraft_IsRejected()
    {
        var setId = await CreateDraftAsync(AddUpload);

        var response = await _client.PostAsJsonAsync($"/api/sets/{setId}/more", new { count = 1, prompt = "a fox" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await GetSetAsync(_client, setId)).IsDraft);
    }

    private Task<HttpResponseMessage> PostDeviantArtDraftAsync(string url) =>
        _client.PostAsJsonAsync("/api/drafts/deviantart", new { url });

    [Fact]
    public async Task DeviantArtDraft_UsesMainImage_AndClosestResolution()
    {
        _factory.DeviantArt.ExtraImages = 2;

        var response = await PostDeviantArtDraftAsync(DeviationLink);

        response.EnsureSuccessStatusCode();
        var setId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var set = await GetSetAsync(_client, setId);
        Assert.True(set.IsDraft);
        Assert.Empty(set.Jobs);
        Assert.Equal(SourceKind.DeviantArt, set.SourceKind);
        Assert.Equal("someartist", set.DeviantArtAuthor);
        Assert.Equal("Wide", set.Resolution);
        Assert.Equal(FakeDeviantArt.JpegBytes, await _client.GetByteArrayAsync(set.SourceImageUrl));
        Assert.Equal([setId], await SetIdsAsync("draft"));
    }

    [Fact]
    public async Task DeviantArtDraft_SameDeviationTwice_IsReportedAsDuplicate()
    {
        var first = await PostDeviantArtDraftAsync(DeviationLink);
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var again = await PostDeviantArtDraftAsync(DeviationLink);

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal(firstId, (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("existingSetId").GetInt32());
    }

    [Fact]
    public async Task DeviantArtDraft_WithOtherUrl_IsRejected()
    {
        var response = await PostDeviantArtDraftAsync("https://example.com/art/thing-123");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await SetIdsAsync("draft"));
    }

    [Fact]
    public async Task DeviationInDraft_IsReportedAsDuplicate()
    {
        var setId = await CreateDraftAsync(AddDeviation);
        Assert.Equal(SourceKind.DeviantArt, (await GetSetAsync(_client, setId)).SourceKind);

        var again = await PostDraftAsync(AddDeviation);

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal(setId, (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("existingSetId").GetInt32());
    }
}
