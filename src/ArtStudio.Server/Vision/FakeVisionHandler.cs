using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace ArtStudio.Server.Vision;

/// <summary>Stands in for the vision server on test instances: answers every request with a fixed prompt after a delay.</summary>
public sealed class FakeVisionHandler(TimeSpan answerTime) : HttpMessageHandler
{
    public const string ConfigurationKey = "ArtStudio:FakeVision";

    public const string Answer =
        "A fox with rust-red fur sits upright on a mossy rock, its tail curled around its front paws.\n\n" +
        "A small grey owl perches on a low branch to the right of the fox, its wings folded.\n\n" +
        "A misty pine forest fills the background, with pale morning light between the trunks.\n\n" +
        "Eye-level view, the fox centred in the lower half, soft light from the left, muted greens and warm oranges.\n\n" +
        "Painterly digital illustration with soft edges, visible brush texture and gentle shading.";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        await Task.Delay(answerTime, ct);
        var body = new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject
            {
                ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = Answer },
                ["finish_reason"] = "stop",
            }),
        };
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
    }
}
