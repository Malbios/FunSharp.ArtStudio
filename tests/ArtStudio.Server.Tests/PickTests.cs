using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Tests.Fakes;
using static ArtStudio.Server.Tests.Fakes.StudioAppFactory;

namespace ArtStudio.Server.Tests;

public sealed class PickTests : IDisposable
{
    private readonly StudioAppFactory _factory = new();
    private readonly HttpClient _client;

    public PickTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private async Task<(int SetId, int[] ImageIds)> CreateSetWithImagesAsync(int count)
    {
        var setId = await _factory.CreateSetAsync(_client, "fox", count: count);
        await WaitUntilAsync(async () => (await GetSetAsync(_client, setId)).Jobs.Single().Status == JobStatus.Completed, "images");
        return (setId, (await GetSetAsync(_client, setId)).Images.Select(i => i.Id).ToArray());
    }

    private async Task PickAsync(int setId, int imageId) =>
        (await _client.PostAsJsonAsync($"/api/sets/{setId}/picks", new { imageId })).EnsureSuccessStatusCode();

    private async Task<IReadOnlyList<int>> PickedAsync(int setId) => (await GetSetAsync(_client, setId)).PickedImageIds;

    [Fact]
    public async Task Picks_KeepTheOrderTheyWereMadeIn_AndIgnoreRepeats()
    {
        var (setId, images) = await CreateSetWithImagesAsync(3);

        await PickAsync(setId, images[2]);
        await PickAsync(setId, images[0]);
        await PickAsync(setId, images[2]);

        Assert.Equal([images[2], images[0]], await PickedAsync(setId));
    }

    [Fact]
    public async Task Unpick_ClosesTheGap()
    {
        var (setId, images) = await CreateSetWithImagesAsync(3);
        foreach (var image in images)
            await PickAsync(setId, image);

        (await _client.DeleteAsync($"/api/sets/{setId}/picks/{images[1]}")).EnsureSuccessStatusCode();
        await PickAsync(setId, images[1]);

        Assert.Equal([images[0], images[2], images[1]], await PickedAsync(setId));
    }

    [Fact]
    public async Task Reorder_SetsTheNewSequence()
    {
        var (setId, images) = await CreateSetWithImagesAsync(3);
        foreach (var image in images)
            await PickAsync(setId, image);

        var response = await _client.PutAsJsonAsync($"/api/sets/{setId}/picks", new { imageIds = new[] { images[1], images[2], images[0] } });

        response.EnsureSuccessStatusCode();
        Assert.Equal([images[1], images[2], images[0]], await PickedAsync(setId));
    }

    [Fact]
    public async Task Reorder_WithDifferentImages_IsRejected()
    {
        var (setId, images) = await CreateSetWithImagesAsync(3);
        await PickAsync(setId, images[0]);
        await PickAsync(setId, images[1]);

        var response = await _client.PutAsJsonAsync($"/api/sets/{setId}/picks", new { imageIds = new[] { images[0], images[2] } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([images[0], images[1]], await PickedAsync(setId));
    }

    [Fact]
    public async Task PickingAnImageFromAnotherSet_IsRejected()
    {
        var (setId, _) = await CreateSetWithImagesAsync(1);
        var (_, otherImages) = await CreateSetWithImagesAsync(1);

        var response = await _client.PostAsJsonAsync($"/api/sets/{setId}/picks", new { imageId = otherImages[0] });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetList_ShowsPickCountAndFirstPickAsPreview()
    {
        var (setId, images) = await CreateSetWithImagesAsync(2);
        await PickAsync(setId, images[1]);
        await PickAsync(setId, images[0]);

        var summary = (await _client.GetFromJsonAsync<JsonElement>("/api/sets")).EnumerateArray().Single();

        Assert.Equal(2, summary.GetProperty("pickedCount").GetInt32());
        Assert.Equal($"/api/images/{images[1]}", summary.GetProperty("previewImageUrl").GetString());
    }

    [Fact]
    public async Task DeletingASet_RemovesItsPicks()
    {
        var (setId, images) = await CreateSetWithImagesAsync(1);
        await PickAsync(setId, images[0]);

        var response = await _client.DeleteAsync($"/api/sets/{setId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
