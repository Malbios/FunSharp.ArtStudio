using System.Net;
using System.Text.Json.Nodes;

namespace ArtStudio.Server.Tests.Fakes;

public sealed record VisionRequest(Uri Uri, string? Authorization, JsonNode Body);

public sealed class FakeVisionServer : HttpMessageHandler
{
    public volatile bool HoldAnswers;
    public volatile bool Unreachable;
    public volatile string Answer = "A fox in the snow.";
    public volatile string FinishReason = "stop";
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public int CancelledRequests { get; private set; }

    public List<VisionRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (Unreachable)
            throw new HttpRequestException("No connection could be made because the target machine actively refused it.");

        var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!;
        lock (Requests)
            Requests.Add(new VisionRequest(request.RequestUri!, request.Headers.Authorization?.ToString(), body));

        try
        {
            while (HoldAnswers)
                await Task.Delay(10, ct);
        }
        catch (OperationCanceledException)
        {
            CancelledRequests++;
            throw;
        }

        if (Status != HttpStatusCode.OK)
            return FakeHttpHandler.Json("""{ "error": { "message": "nope" } }""", Status);

        var answer = new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject
            {
                ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = Answer },
                ["finish_reason"] = FinishReason,
            }),
        };
        return FakeHttpHandler.Json(answer.ToJsonString());
    }
}
