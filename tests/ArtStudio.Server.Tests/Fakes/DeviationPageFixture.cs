using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArtStudio.Server.Tests.Fakes;

public static class DeviationPageFixture
{
    private static readonly JsonSerializerOptions RelaxedEscaping = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string ExtraImageUrl(int position) =>
        $"https://images.example.test/extra-{position}.png/v1/fill/w_800,h_1067,q_80,strp/extra_{position}-fullview.jpg?token=token-{position}";

    public static byte[] ExtraImageBytes(int position) => [0xFF, 0xD8, 0xFF, (byte)(0xE0 + position)];

    /// <summary>Builds a deviation page shaped like DeviantArt's: a meta tag with the UUID and the page state as a JS string.</summary>
    public static string Html(string uuid, string deviationNumber, int extraImages, bool blurred = false)
    {
        var additionalMedia = new JsonArray();
        // Listed in reverse to prove the parser orders by position.
        for (var position = extraImages; position >= 1; position--)
        {
            var transform = blurred ? "w_800,h_1067,q_80,strp,blur_39" : "w_800,h_1067,q_80,strp";
            additionalMedia.Add(new JsonObject
            {
                ["position"] = position,
                ["media"] = new JsonObject
                {
                    ["baseUri"] = $"https://images.example.test/extra-{position}.png",
                    ["prettyName"] = $"extra_{position}",
                    ["token"] = new JsonArray($"token-{position}"),
                    ["types"] = new JsonArray(
                        new JsonObject { ["t"] = "150", ["c"] = "/v1/fit/w_150,h_150/<prettyName>-150.jpg", ["w"] = 112, ["h"] = 150 },
                        new JsonObject { ["t"] = "fullview", ["c"] = $"/v1/fill/{transform}/<prettyName>-fullview.jpg", ["w"] = 800, ["h"] = 1067 }),
                },
            });
        }

        var state = new JsonObject
        {
            ["@@entities"] = new JsonObject
            {
                ["deviation"] = new JsonObject { [deviationNumber] = new JsonObject { ["title"] = "It's a test" } },
                ["deviationExtended"] = new JsonObject { [deviationNumber] = new JsonObject { ["additionalMedia"] = additionalMedia } },
            },
        };

        // DeviantArt embeds the state as a JavaScript string, which also uses \' escapes that JSON does not allow.
        var javaScriptString = JsonSerializer.Serialize(state.ToJsonString(RelaxedEscaping), RelaxedEscaping).Replace("'", "\\'");
        return $"""
            <html><head><meta property="da:appurl" content="DeviantArt://deviation/{uuid}"/></head>
            <body><script>window.__INITIAL_STATE__ = JSON.parse({javaScriptString});</script></body></html>
            """;
    }
}
