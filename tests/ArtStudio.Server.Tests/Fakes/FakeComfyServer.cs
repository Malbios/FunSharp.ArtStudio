using System.Net;
using System.Text.Json.Nodes;

namespace ArtStudio.Server.Tests.Fakes;

public sealed class FakeComfyServer : HttpMessageHandler
{
    public static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private int _promptCounter;

    public volatile bool HoldRuns;
    public volatile bool FailRuns;
    public volatile string? CurrentPromptId;

    public List<string> SubmittedPrompts { get; } = [];
    public List<string> DeletedPromptIds { get; } = [];
    public int InterruptCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);

        lock (this)
        {
            return (request.Method.Method, path) switch
            {
                ("POST", "/prompt") => QueuePrompt(body!),
                ("GET", var p) when p.StartsWith("/history/") => History(p["/history/".Length..]),
                ("GET", "/view") => FakeHttpHandler.Bytes(PngBytes),
                ("GET", "/queue") => FakeHttpHandler.Json(
                    $$"""{ "queue_running": [[1, "{{CurrentPromptId}}"]], "queue_pending": [] }"""),
                ("POST", "/queue") => DeleteFromQueue(body!),
                ("POST", "/interrupt") => Interrupt(),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }
    }

    private HttpResponseMessage QueuePrompt(string body)
    {
        var workflow = JsonNode.Parse(body)!["prompt"]!;
        SubmittedPrompts.Add(workflow["79:8"]!["inputs"]!["text"]!.GetValue<string>());
        CurrentPromptId = $"prompt-{++_promptCounter}";
        return FakeHttpHandler.Json($$"""{ "prompt_id": "{{CurrentPromptId}}", "node_errors": {} }""");
    }

    private HttpResponseMessage History(string promptId)
    {
        if (HoldRuns)
            return FakeHttpHandler.Json("{}");
        if (FailRuns)
            return FakeHttpHandler.Json($$"""{ "{{promptId}}": { "status": { "status_str": "error", "completed": false, "messages": [] } } }""");
        return FakeHttpHandler.Json($$"""
            {
              "{{promptId}}": {
                "status": { "status_str": "success", "completed": true, "messages": [] },
                "outputs": { "78": { "images": [ { "filename": "{{promptId}}.png", "subfolder": "", "type": "output" } ] } }
              }
            }
            """);
    }

    private HttpResponseMessage DeleteFromQueue(string body)
    {
        DeletedPromptIds.Add(JsonNode.Parse(body)!["delete"]![0]!.GetValue<string>());
        return FakeHttpHandler.Json("{}");
    }

    private HttpResponseMessage Interrupt()
    {
        InterruptCount++;
        return FakeHttpHandler.Json("{}");
    }
}
