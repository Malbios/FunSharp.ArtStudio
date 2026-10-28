using System.Net;
using System.Text.Json.Nodes;

namespace ArtStudio.Server.Tests.Fakes;

public sealed class FakeComfyServer : HttpMessageHandler
{
    public static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    public const string VideoSubfolder = "video";

    private int _promptCounter;

    public volatile bool HoldRuns;
    public volatile bool FailRuns;
    public volatile string? CurrentPromptId;

    public List<string> SubmittedPrompts { get; } = [];
    public List<string> SubmittedHosts { get; } = [];
    public List<long> SubmittedSeeds { get; } = [];
    public List<JsonObject> SubmittedVideoWorkflows { get; } = [];
    public List<string> UploadedFileNames { get; } = [];
    public List<string> ViewedFileNames { get; } = [];
    private readonly HashSet<string> _knownPromptIds = [];
    private readonly Dictionary<string, string> _videoFileNames = [];
    private readonly HashSet<string> _reportedVideos = [];
    public List<string> DeletedPromptIds { get; } = [];
    public int InterruptCount { get; private set; }

    /// <summary>Raised the first time a video run is reported done, with its subfolder and file name.</summary>
    public event Action<string, string>? VideoRendered;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        (string Subfolder, string Name)? rendered = null;
        HttpResponseMessage response;

        lock (this)
        {
            response = (request.Method.Method, path) switch
            {
                ("POST", "/prompt") => QueuePrompt(request.RequestUri.Authority, body!),
                ("POST", "/upload/image") => Upload(request.Content as MultipartFormDataContent),
                ("GET", var p) when p.StartsWith("/history/") => History(p["/history/".Length..], out rendered),
                ("GET", "/view") => View(request.RequestUri),
                ("GET", "/queue") => FakeHttpHandler.Json(
                    $$"""{ "queue_running": [[1, "{{CurrentPromptId}}"]], "queue_pending": [] }"""),
                ("POST", "/queue") => DeleteFromQueue(body!),
                ("POST", "/interrupt") => Interrupt(),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }

        if (rendered is { } video)
            VideoRendered?.Invoke(video.Subfolder, video.Name);
        return response;
    }

    private HttpResponseMessage QueuePrompt(string host, string body)
    {
        SubmittedHosts.Add(host);
        var workflow = JsonNode.Parse(body)!["prompt"]!.AsObject();
        CurrentPromptId = $"prompt-{++_promptCounter}";
        _knownPromptIds.Add(CurrentPromptId);

        if (workflow["92"]?["inputs"]?["filename_prefix"]?.GetValue<string>() is { } prefix)
        {
            SubmittedVideoWorkflows.Add(workflow.DeepClone().AsObject());
            SubmittedPrompts.Add(workflow["135:105:104"]!["inputs"]!["prompt"]!.GetValue<string>());
            SubmittedSeeds.Add(workflow["135:223:139"]!["inputs"]!["seed"]!.GetValue<long>());
            _videoFileNames[CurrentPromptId] = $"{prefix.Split('/').Last()}_00001_.mp4";
        }
        else
        {
            SubmittedPrompts.Add(workflow["79:8"]!["inputs"]!["text"]!.GetValue<string>());
            SubmittedSeeds.Add(workflow["79:129"]!["inputs"]!["seed"]!.GetValue<long>());
        }
        return FakeHttpHandler.Json($$"""{ "prompt_id": "{{CurrentPromptId}}", "node_errors": {} }""");
    }

    private HttpResponseMessage Upload(MultipartFormDataContent? form)
    {
        var fileName = form?.Select(part => part.Headers.ContentDisposition?.FileName?.Trim('"')).FirstOrDefault(name => name is not null);
        if (fileName is null)
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        UploadedFileNames.Add(fileName);
        return FakeHttpHandler.Json($$"""{ "name": "{{fileName}}", "subfolder": "", "type": "input" }""");
    }

    private HttpResponseMessage History(string promptId, out (string, string)? renderedVideo)
    {
        renderedVideo = null;
        if (HoldRuns || !_knownPromptIds.Contains(promptId))
            return FakeHttpHandler.Json("{}");
        if (FailRuns)
            return FakeHttpHandler.Json($$"""{ "{{promptId}}": { "status": { "status_str": "error", "completed": false, "messages": [] } } }""");

        if (_videoFileNames.TryGetValue(promptId, out var videoFileName))
        {
            if (_reportedVideos.Add(promptId))
                renderedVideo = (VideoSubfolder, videoFileName);
            return FakeHttpHandler.Json($$"""
                {
                  "{{promptId}}": {
                    "status": { "status_str": "success", "completed": true, "messages": [] },
                    "outputs": { "92": { "images": [ { "filename": "{{videoFileName}}", "subfolder": "{{VideoSubfolder}}", "type": "output" } ], "animated": [true] } }
                  }
                }
                """);
        }

        return FakeHttpHandler.Json($$"""
            {
              "{{promptId}}": {
                "status": { "status_str": "success", "completed": true, "messages": [] },
                "outputs": { "78": { "images": [ { "filename": "{{promptId}}.png", "subfolder": "", "type": "output" } ] } }
              }
            }
            """);
    }

    private HttpResponseMessage View(Uri uri)
    {
        ViewedFileNames.Add(System.Web.HttpUtility.ParseQueryString(uri.Query)["filename"] ?? "");
        return FakeHttpHandler.Bytes(PngBytes);
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
