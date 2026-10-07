using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Tests.Fakes;
using static ArtStudio.Server.Tests.Fakes.StudioAppFactory;

namespace ArtStudio.Server.Tests;

public sealed class ArchiveTests : IDisposable
{
    private const string DeviationLink = "https://www.deviantart.com/someartist/art/Misty-Forest-123456";

    private readonly StudioAppFactory _factory = new();
    private readonly HttpClient _client;

    public ArchiveTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private async Task<(int SetId, int[] ImageIds)> CreateCompletedSetAsync(int count = 1)
    {
        var setId = await _factory.CreateSetAsync(_client, "fox", count: count);
        await WaitForJobsAsync(setId, JobStatus.Completed);
        return (setId, (await GetSetAsync(_client, setId)).Images.Select(i => i.Id).ToArray());
    }

    private Task WaitForJobsAsync(int setId, JobStatus status) =>
        WaitUntilAsync(async () => (await GetSetAsync(_client, setId)).Jobs.All(j => j.Status == status), $"jobs {status}");

    private Task<HttpResponseMessage> ArchiveAsync(int setId) => _client.PostAsync($"/api/sets/{setId}/archive", null);

    private Task<HttpResponseMessage> RestoreAsync(int setId) => _client.DeleteAsync($"/api/sets/{setId}/archive");

    private async Task<int[]> SetIdsAsync(string stage) =>
        (await _client.GetFromJsonAsync<JsonElement>($"/api/sets?stage={stage}"))
            .EnumerateArray().Select(s => s.GetProperty("id").GetInt32()).ToArray();

    [Fact]
    public async Task WorkingSet_MovesToArchive_AndBackToSets()
    {
        var (setId, _) = await CreateCompletedSetAsync();

        (await ArchiveAsync(setId)).EnsureSuccessStatusCode();

        Assert.Empty(await SetIdsAsync("working"));
        Assert.Equal([setId], await SetIdsAsync("archived"));
        Assert.NotNull((await GetSetAsync(_client, setId)).ArchivedAt);

        (await RestoreAsync(setId)).EnsureSuccessStatusCode();

        Assert.Equal([setId], await SetIdsAsync("working"));
        Assert.Empty(await SetIdsAsync("archived"));
        Assert.Null((await GetSetAsync(_client, setId)).ArchivedAt);
    }

    [Fact]
    public async Task ReadySet_MovesToArchive_AndBackToPost()
    {
        var (setId, images) = await CreateCompletedSetAsync();
        (await _client.PostAsJsonAsync($"/api/sets/{setId}/picks", new { imageId = images[0] })).EnsureSuccessStatusCode();
        (await _client.PostAsync($"/api/sets/{setId}/ready", null)).EnsureSuccessStatusCode();

        (await ArchiveAsync(setId)).EnsureSuccessStatusCode();

        Assert.Empty(await SetIdsAsync("ready"));
        Assert.Equal([setId], await SetIdsAsync("archived"));

        (await RestoreAsync(setId)).EnsureSuccessStatusCode();

        Assert.Equal([setId], await SetIdsAsync("ready"));
        Assert.Empty(await SetIdsAsync("working"));
        Assert.Equal([images[0]], (await GetSetAsync(_client, setId)).PickedImageIds);
    }

    [Fact]
    public async Task ArchivedSet_RefusesGeneratingAndStageChanges()
    {
        var (setId, images) = await CreateCompletedSetAsync();
        (await _client.PostAsJsonAsync($"/api/sets/{setId}/picks", new { imageId = images[0] })).EnsureSuccessStatusCode();
        (await ArchiveAsync(setId)).EnsureSuccessStatusCode();

        var more = await _client.PostAsJsonAsync($"/api/sets/{setId}/more", new { count = 1 });
        var ready = await _client.PostAsync($"/api/sets/{setId}/ready", null);
        var backToSets = await _client.DeleteAsync($"/api/sets/{setId}/ready");
        var again = await ArchiveAsync(setId);

        Assert.All([more, ready, backToSets, again], response => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode));
        var set = await GetSetAsync(_client, setId);
        Assert.Single(set.Jobs);
        Assert.Null(set.ReadyToPostAt);
        Assert.Equal([setId], await SetIdsAsync("archived"));
    }

    [Fact]
    public async Task Archiving_StopsRunningAndCancelsQueuedJobs()
    {
        _factory.Comfy.HoldRuns = true;
        var setId = await _factory.CreateSetAsync(_client, "slow", count: 2);
        await WaitUntilAsync(() => Task.FromResult(_factory.Comfy.SubmittedPrompts.Count == 1), "prompt submitted");
        (await _client.PostAsJsonAsync($"/api/sets/{setId}/more", new { count = 1 })).EnsureSuccessStatusCode();

        (await ArchiveAsync(setId)).EnsureSuccessStatusCode();

        Assert.All((await GetSetAsync(_client, setId)).Jobs, job => Assert.Equal(JobStatus.Cancelled, job.Status));
        Assert.Empty((await GetQueueAsync(_client)).Active);
    }

    [Fact]
    public async Task Archiving_CancelsFailedJobs()
    {
        _factory.Comfy.FailRuns = true;
        var setId = await _factory.CreateSetAsync(_client, "fails", count: 1);
        await WaitForJobsAsync(setId, JobStatus.Failed);

        (await ArchiveAsync(setId)).EnsureSuccessStatusCode();

        Assert.Equal(JobStatus.Cancelled, (await GetSetAsync(_client, setId)).Jobs.Single().Status);
        Assert.Empty((await GetQueueAsync(_client)).Active);
    }

    [Fact]
    public async Task ArchivedDeviation_StaysUsed_UntilTheSetIsDeleted()
    {
        var setId = await CreateFromDeviationAsync();
        await WaitForJobsAsync(setId, JobStatus.Completed);
        (await ArchiveAsync(setId)).EnsureSuccessStatusCode();

        var duplicate = await PostFromDeviationAsync();
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(setId, (await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("existingSetId").GetInt32());

        (await _client.DeleteAsync($"/api/sets/{setId}")).EnsureSuccessStatusCode();
        (await PostFromDeviationAsync()).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Draft_CannotBeArchived()
    {
        var response = await _client.PostAsJsonAsync("/api/drafts/deviantart", new { url = DeviationLink });
        var draftId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.BadRequest, (await ArchiveAsync(draftId)).StatusCode);
        Assert.Equal([draftId], await SetIdsAsync("draft"));
    }

    private Task<HttpResponseMessage> PostFromDeviationAsync()
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("forest"), "prompt" },
            { new StringContent("Wide"), "resolution" },
            { new StringContent("1"), "count" },
            { new StringContent("DeviantArt"), "sourceKind" },
            { new StringContent(DeviationLink), "deviantArtUrl" },
        };
        return _client.PostAsync("/api/sets", form);
    }

    private async Task<int> CreateFromDeviationAsync()
    {
        var response = await PostFromDeviationAsync();
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }
}
