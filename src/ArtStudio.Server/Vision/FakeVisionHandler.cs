using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ArtStudio.Server.Vision;

/// <summary>
/// Stands in for the vision server on test instances: describes every image with a fixed prompt, and answers
/// text-only modify requests with the text they carry, marked as modified.
/// </summary>
public sealed partial class FakeVisionHandler(TimeSpan imageAnswerTime, TimeSpan textAnswerTime) : HttpMessageHandler
{
    public const string ConfigurationKey = "ArtStudio:FakeVision";
    public const string ModifiedMarker = " (modified)";

    public const string Answer =
        "A fox with rust-red fur sits upright on a mossy rock, its tail curled around its front paws.\n\n" +
        "A small grey owl perches on a low branch to the right of the fox, its wings folded.\n\n" +
        "A misty pine forest fills the background, with pale morning light between the trunks.\n\n" +
        "Eye-level view, the fox centred in the lower half, soft light from the left, muted greens and warm oranges.\n\n" +
        "Painterly digital illustration with soft edges, visible brush texture and gentle shading.";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var content = JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!["messages"]![0]!["content"]!.AsArray();
        var hasImage = content.Any(part => part?["type"]?.GetValue<string>() == "image_url");
        await Task.Delay(hasImage ? imageAnswerTime : textAnswerTime, ct);

        var answer = hasImage ? Answer : ModifiedText(content[0]!["text"]!.GetValue<string>());
        var body = new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject
            {
                ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = answer },
                ["finish_reason"] = "stop",
            }),
        };
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
    }

    private static string ModifiedText(string instruction)
    {
        var match = TaggedText().Match(instruction);
        return (match.Success ? match.Groups[1].Value.Trim() : instruction) + ModifiedMarker;
    }

    [GeneratedRegex(@"<(?:prompt|paragraph)>(.*?)</(?:prompt|paragraph)>", RegexOptions.Singleline)]
    private static partial Regex TaggedText();
}
