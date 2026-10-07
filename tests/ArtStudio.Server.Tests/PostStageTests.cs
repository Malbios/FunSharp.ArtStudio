using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Tests.Fakes;
using static ArtStudio.Server.Tests.Fakes.StudioAppFactory;

namespace ArtStudio.Server.Tests;

public sealed class PostStageTests : IDisposable
{
    private readonly StudioAppFactory _factory = new();
    private readonly HttpClient _client;

    public PostStageTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private async Task<(int SetId, int[] ImageIds)> CreateCompletedSetAsync(int count)
    {
        var setId = await _factory.CreateSetAsync(_client, "fox", count: count);
        await WaitUntilAsync(async () => (await GetSetAsync(_client, setId)).Jobs.All(j => j.Status == JobStatus.Completed), "images");
        return (setId, (await GetSetAsync(_client, setId)).Images.Select(i => i.Id).ToArray());
    }

    private async Task PickAsync(int setId, int imageId) =>
        (await _client.PostAsJsonAsync($"/api/sets/{setId}/picks", new { imageId })).EnsureSuccessStatusCode();

    private Task<HttpResponseMessage> MarkReadyAsync(int setId) => _client.PostAsync($"/api/sets/{setId}/ready", null);

    private async Task<int[]> SetIdsAsync(string stage) =>
        (await _client.GetFromJsonAsync<JsonElement>($"/api/sets?stage={stage}"))
            .EnumerateArray().Select(s => s.GetProperty("id").GetInt32()).ToArray();

    [Fact]
    public async Task ReadySet_MovesFromSetsToPost_AndBack()
    {
        var (setId, images) = await CreateCompletedSetAsync(1);
        await PickAsync(setId, images[0]);

        (await MarkReadyAsync(setId)).EnsureSuccessStatusCode();

        Assert.Empty(await SetIdsAsync("working"));
        Assert.Equal([setId], await SetIdsAsync("ready"));
        Assert.NotNull((await GetSetAsync(_client, setId)).ReadyToPostAt);

        (await _client.DeleteAsync($"/api/sets/{setId}/ready")).EnsureSuccessStatusCode();

        Assert.Equal([setId], await SetIdsAsync("working"));
        Assert.Empty(await SetIdsAsync("ready"));
    }

    [Fact]
    public async Task SetListWithoutStage_ShowsWorkingSets()
    {
        var (setId, _) = await CreateCompletedSetAsync(1);

        var ids = (await _client.GetFromJsonAsync<JsonElement>("/api/sets")).EnumerateArray().Select(s => s.GetProperty("id").GetInt32());

        Assert.Equal([setId], ids);
    }

    [Fact]
    public async Task MarkReady_WithoutPicks_IsRejected()
    {
        var (setId, _) = await CreateCompletedSetAsync(1);

        var response = await MarkReadyAsync(setId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Pick at least one", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }

    [Fact]
    public async Task MarkReady_WithActiveJob_IsRejected()
    {
        var (setId, images) = await CreateCompletedSetAsync(1);
        await PickAsync(setId, images[0]);
        _factory.Comfy.HoldRuns = true;
        (await _client.PostAsJsonAsync($"/api/sets/{setId}/more", new { count = 1 })).EnsureSuccessStatusCode();

        var response = await MarkReadyAsync(setId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null((await GetSetAsync(_client, setId)).ReadyToPostAt);
    }

    [Fact]
    public async Task ReadySet_RefusesNewImagesUntilMovedBack()
    {
        var (setId, images) = await CreateCompletedSetAsync(1);
        await PickAsync(setId, images[0]);
        (await MarkReadyAsync(setId)).EnsureSuccessStatusCode();

        var refused = await _client.PostAsJsonAsync($"/api/sets/{setId}/more", new { count = 1, prompt = "an owl" });
        (await _client.DeleteAsync($"/api/sets/{setId}/ready")).EnsureSuccessStatusCode();
        var allowed = await _client.PostAsJsonAsync($"/api/sets/{setId}/more", new { count = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        allowed.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ReadySet_KeepsAtLeastOnePick()
    {
        var (setId, images) = await CreateCompletedSetAsync(2);
        await PickAsync(setId, images[0]);
        await PickAsync(setId, images[1]);
        (await MarkReadyAsync(setId)).EnsureSuccessStatusCode();

        (await _client.DeleteAsync($"/api/sets/{setId}/picks/{images[0]}")).EnsureSuccessStatusCode();
        var lastPick = await _client.DeleteAsync($"/api/sets/{setId}/picks/{images[1]}");

        Assert.Equal(HttpStatusCode.BadRequest, lastPick.StatusCode);
        Assert.Equal([images[1]], (await GetSetAsync(_client, setId)).PickedImageIds);
    }

    [Fact]
    public async Task ReadySet_CanBeDeleted()
    {
        var (setId, images) = await CreateCompletedSetAsync(1);
        await PickAsync(setId, images[0]);
        (await MarkReadyAsync(setId)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/sets/{setId}")).StatusCode);
        Assert.Empty(await SetIdsAsync("ready"));
    }
}
