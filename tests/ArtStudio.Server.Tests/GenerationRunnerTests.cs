using System.Net;
using System.Text.Json.Nodes;
using ArtStudio.Server.Comfy;
using ArtStudio.Server.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArtStudio.Server.Tests;

public class GenerationRunnerTests
{
    private const string PromptId = "abc-123";
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47];

    private const string QueuedResponse = $$"""{ "prompt_id": "{{PromptId}}", "number": 1, "node_errors": {} }""";

    private const string CompletedHistory = $$"""
        {
          "{{PromptId}}": {
            "status": { "status_str": "success", "completed": true, "messages": [] },
            "outputs": { "78": { "images": [ { "filename": "Krea2_00001_.png", "subfolder": "", "type": "output" } ] } }
          }
        }
        """;

    private static readonly GenerationTiming FastTiming = new(TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(5));

    private static GenerationRunner CreateRunner(GenerationTiming? timing = null) =>
        new(timing ?? FastTiming, NullLogger<GenerationRunner>.Instance);

    private static FakeHttpHandler ComfyServer(Func<int, string> history, string queued = QueuedResponse)
    {
        var historyCalls = 0;
        return new FakeHttpHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/prompt" => FakeHttpHandler.Json(queued),
            var path when path.StartsWith("/history/") => FakeHttpHandler.Json(history(++historyCalls)),
            "/view" => FakeHttpHandler.Bytes(PngBytes),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
    }

    [Fact]
    public async Task RunAsync_PollsUntilCompletedAndDownloadsImages()
    {
        var server = ComfyServer(call => call < 3 ? "{}" : CompletedHistory);
        string? queuedId = null;

        var images = await CreateRunner().RunAsync(
            new ComfyClient(server.CreateClient()), new JsonObject(), id => { queuedId = id; return Task.CompletedTask; }, CancellationToken.None);

        Assert.Equal(PromptId, queuedId);
        var image = Assert.Single(images);
        Assert.Equal("Krea2_00001_.png", image.FileName);
        Assert.Equal(PngBytes, image.Content);
        Assert.Equal(3, server.Requests.Count(r => r.PathAndQuery.StartsWith("/history/")));
        Assert.Contains(server.Requests, r => r.PathAndQuery == "/view?filename=Krea2_00001_.png&subfolder=&type=output");
    }

    [Fact]
    public async Task RunAsync_SubmitsWorkflowAsPromptPayload()
    {
        var server = ComfyServer(_ => CompletedHistory);

        await CreateRunner().RunAsync(
            new ComfyClient(server.CreateClient()), new JsonObject { ["1"] = "node" }, _ => Task.CompletedTask, CancellationToken.None);

        var submitted = JsonNode.Parse(server.Requests.Single(r => r.PathAndQuery == "/prompt").Body!)!;
        Assert.Equal("node", submitted["prompt"]!["1"]!.GetValue<string>());
        Assert.False(string.IsNullOrEmpty(submitted["client_id"]!.GetValue<string>()));
    }

    [Fact]
    public async Task RunAsync_ErrorStatus_Throws()
    {
        var server = ComfyServer(_ => $$"""{ "{{PromptId}}": { "status": { "status_str": "error", "completed": false, "messages": [] } } }""");

        await Assert.ThrowsAsync<ComfyException>(() => CreateRunner().RunAsync(
            new ComfyClient(server.CreateClient()), new JsonObject(), _ => Task.CompletedTask, CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_InterruptedMessage_Throws()
    {
        var server = ComfyServer(_ => $$"""
            { "{{PromptId}}": { "status": { "status_str": "success", "completed": false,
              "messages": [ ["execution_start", {}], ["execution_interrupted", { "node_id": "79:9" }] ] } } }
            """);

        var error = await Assert.ThrowsAsync<ComfyException>(() => CreateRunner().RunAsync(
            new ComfyClient(server.CreateClient()), new JsonObject(), _ => Task.CompletedTask, CancellationToken.None));
        Assert.Contains("execution_interrupted", error.Message);
    }

    [Fact]
    public async Task RunAsync_MissingPromptId_Throws()
    {
        var server = ComfyServer(_ => CompletedHistory, queued: """{ "error": "invalid prompt" }""");

        var error = await Assert.ThrowsAsync<ComfyException>(() => CreateRunner().RunAsync(
            new ComfyClient(server.CreateClient()), new JsonObject(), _ => Task.CompletedTask, CancellationToken.None));
        Assert.Contains("invalid prompt", error.Message);
    }

    [Fact]
    public async Task RunAsync_CompletedWithoutImages_Throws()
    {
        var server = ComfyServer(_ => $$"""{ "{{PromptId}}": { "status": { "completed": true, "messages": [] }, "outputs": {} } }""");

        await Assert.ThrowsAsync<ComfyException>(() => CreateRunner().RunAsync(
            new ComfyClient(server.CreateClient()), new JsonObject(), _ => Task.CompletedTask, CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_NeverCompletes_TimesOut()
    {
        var server = ComfyServer(_ => "{}");
        var timing = new GenerationTiming(TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(50));

        var error = await Assert.ThrowsAsync<ComfyException>(() => CreateRunner(timing).RunAsync(
            new ComfyClient(server.CreateClient()), new JsonObject(), _ => Task.CompletedTask, CancellationToken.None));
        Assert.Contains("Timed out", error.Message);
    }

    [Fact]
    public async Task RunAsync_ServerError_ThrowsComfyException()
    {
        var server = new FakeHttpHandler(_ => FakeHttpHandler.Json("""{ "error": "boom" }""", HttpStatusCode.InternalServerError));

        var error = await Assert.ThrowsAsync<ComfyException>(() => CreateRunner().RunAsync(
            new ComfyClient(server.CreateClient()), new JsonObject(), _ => Task.CompletedTask, CancellationToken.None));
        Assert.Contains("500", error.Message);
    }
}
