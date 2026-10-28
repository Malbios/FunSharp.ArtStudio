using System.Net;
using System.Net.Http.Json;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Generation;
using ArtStudio.Server.Tests.Fakes;
using static ArtStudio.Server.Tests.Fakes.StudioAppFactory;

namespace ArtStudio.Server.Tests;

public sealed class ParagraphOrderTests : IDisposable
{
    private const string Prompt = "A fox.\n\nAn owl.\n\nA forest.\n\nEye level.\n\nSoft watercolour.";
    private const string StyleFirst = "Style,Characters,Setting,Composition";

    private readonly StudioAppFactory _factory = new();
    private readonly HttpClient _client;

    public ParagraphOrderTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private async Task<int> CreateSetAsync(string? paragraphOrder)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(Prompt), "prompt" },
            { new StringContent("Native"), "resolution" },
            { new StringContent("1"), "count" },
        };
        if (paragraphOrder is not null)
            form.Add(new StringContent(paragraphOrder), "paragraphOrder");
        var response = await _client.PostAsync("/api/sets", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetInt32();
    }

    private async Task WaitForImagesAsync(int setId, int count) =>
        await WaitUntilAsync(async () => (await GetSetAsync(_client, setId)).Images.Count == count, $"{count} images");

    [Fact]
    public void Apply_MovesTheSectionsAndKeepsTheCharactersTogether()
    {
        Assert.Equal(
            "Soft watercolour.\n\nA fox.\n\nAn owl.\n\nA forest.\n\nEye level.",
            ParagraphOrder.Apply(Prompt, StyleFirst));
        Assert.Equal(
            "A forest.\n\nA fox.\n\nAn owl.\n\nSoft watercolour.\n\nEye level.",
            ParagraphOrder.Apply(Prompt, "Setting,Characters,Style,Composition"));
    }

    [Fact]
    public void Apply_WithoutCharacters_OrdersTheThreeSections()
    {
        Assert.Equal(
            "Soft watercolour.\n\nA forest.\n\nEye level.",
            ParagraphOrder.Apply("A forest.\n\nEye level.\n\nSoft watercolour.", StyleFirst));
    }

    [Fact]
    public void Apply_LeavesPromptsWithoutTheSectionsAndTheNormalOrderAlone()
    {
        Assert.Equal("A fox.\n\nWatercolour.", ParagraphOrder.Apply("A fox.\n\nWatercolour.", StyleFirst));
        Assert.Equal(Prompt, ParagraphOrder.Apply(Prompt, null));
        Assert.Equal(Prompt, ParagraphOrder.Apply(Prompt, ParagraphOrder.Normal));
    }

    [Theory]
    [InlineData("Style,Characters,Setting")]
    [InlineData("Style,Style,Setting,Composition")]
    [InlineData("Style,Characters,Setting,Lighting")]
    [InlineData("Style,Characters,Setting,Composition,Characters")]
    public void Normalize_RejectsAnythingButEachSectionOnce(string order) =>
        Assert.Throws<UserFacingException>(() => ParagraphOrder.Normalize(order));

    [Fact]
    public void Normalize_StoresTheNormalOrderAsNull()
    {
        Assert.Null(ParagraphOrder.Normalize(ParagraphOrder.Normal));
        Assert.Null(ParagraphOrder.Normalize(" "));
        Assert.Equal(StyleFirst, ParagraphOrder.Normalize("Style, Characters, Setting, Composition"));
    }

    [Fact]
    public async Task Run_SendsTheReorderedPrompt_ButKeepsTheNormalPromptEverywhereElse()
    {
        var setId = await CreateSetAsync(StyleFirst);
        await WaitForImagesAsync(setId, 1);

        var set = await GetSetAsync(_client, setId);
        Assert.Equal([ParagraphOrder.Apply(Prompt, StyleFirst)], _factory.Comfy.SubmittedPrompts);
        Assert.Equal((Prompt, StyleFirst), (set.Jobs.Single().Prompt, set.Jobs.Single().ParagraphOrder));
        Assert.Equal((Prompt, StyleFirst), (set.Images.Single().Prompt, set.Images.Single().ParagraphOrder));
        Assert.Equal((Prompt, StyleFirst), (set.Prompt, set.ParagraphOrder));
    }

    [Fact]
    public async Task MoreImages_WithoutAnOrder_ReuseTheSetsLatestOne_AndTheNormalOrderResetsIt()
    {
        var setId = await CreateSetAsync(StyleFirst);
        await WaitForImagesAsync(setId, 1);

        (await _client.PostAsJsonAsync($"/api/sets/{setId}/more", new { count = 1 })).EnsureSuccessStatusCode();
        await WaitForImagesAsync(setId, 2);
        (await _client.PostAsJsonAsync($"/api/sets/{setId}/more", new { count = 1, paragraphOrder = ParagraphOrder.Normal }))
            .EnsureSuccessStatusCode();
        await WaitForImagesAsync(setId, 3);

        var set = await GetSetAsync(_client, setId);
        Assert.Equal([StyleFirst, StyleFirst, null], set.Jobs.OrderBy(j => j.Id).Select(j => j.ParagraphOrder));
        Assert.Null(set.ParagraphOrder);
        Assert.Equal(Prompt, _factory.Comfy.SubmittedPrompts.Last());
    }

    [Fact]
    public async Task InvalidOrder_IsRejected_WithoutQueueing()
    {
        var setId = await CreateSetAsync(paragraphOrder: null);
        await WaitForImagesAsync(setId, 1);

        var response = await _client.PostAsJsonAsync($"/api/sets/{setId}/more", new { count = 1, paragraphOrder = "Style" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single((await GetSetAsync(_client, setId)).Jobs);
    }

    [Fact]
    public async Task ImagesAfterAModification_UseTheOrderAskedFor()
    {
        await SetVisionApiKeyAsync(_client);
        var setId = await CreateSetAsync(paragraphOrder: null);
        await WaitForImagesAsync(setId, 1);
        _factory.Vision.Answer = Prompt;

        (await _client.PostAsJsonAsync($"/api/sets/{setId}/modify-prompt", new
        {
            prompt = Prompt, instructions = "Keep it.", imageCount = 1, resolution = "Native", paragraphOrder = StyleFirst,
        })).EnsureSuccessStatusCode();
        await WaitUntilAsync(async () => (await GetSetAsync(_client, setId)).Jobs.Count == 2, "a second job");

        var job = (await GetSetAsync(_client, setId)).Jobs.MaxBy(j => j.Id)!;
        Assert.Equal(StyleFirst, job.ParagraphOrder);
        Assert.NotEqual(JobStatus.Failed, job.Status);
    }
}
