using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ArtStudio.Server.DeviantArt;

public sealed record PageImage(int Position, string FullviewUrl, int Width, int Height, bool Blurred);

/// <summary>
/// Reads what the official API does not offer from a deviation's public web page: its UUID and the extra images of
/// multi-image posts. The embedded page state is undocumented and may change at any time, so parsing failures yield
/// no extra images instead of an error.
/// </summary>
public sealed partial record DeviationPage(string? Uuid, IReadOnlyList<PageImage> AdditionalImages, bool StateParsed)
{
    private const string StateMarker = "window.__INITIAL_STATE__ = JSON.parse(\"";
    private const string FullviewType = "fullview";

    [GeneratedRegex(@"DeviantArt://deviation/(?<uuid>[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})")]
    private static partial Regex DeviationUuid();

    public static DeviationPage Parse(string html, string deviationNumber)
    {
        var uuidMatch = DeviationUuid().Match(html);
        var uuid = uuidMatch.Success ? uuidMatch.Groups["uuid"].Value : null;

        var state = ReadInitialState(html);
        if (state is null)
            return new DeviationPage(uuid, [], StateParsed: false);

        var additionalMedia = state["@@entities"]?["deviationExtended"]?[deviationNumber]?["additionalMedia"] as JsonArray ?? [];
        var images = additionalMedia
            .Select(ReadImage)
            .OfType<PageImage>()
            .OrderBy(image => image.Position)
            .ToList();
        return new DeviationPage(uuid, images, StateParsed: true);
    }

    private static PageImage? ReadImage(JsonNode? item)
    {
        var media = item?["media"];
        var fullview = (media?["types"] as JsonArray)?.FirstOrDefault(type => type?["t"]?.GetValue<string>() == FullviewType);
        var path = fullview?["c"]?.GetValue<string>();
        var baseUri = media?["baseUri"]?.GetValue<string>();
        var prettyName = media?["prettyName"]?.GetValue<string>();
        if (path is null || baseUri is null || prettyName is null)
            return null;

        var token = (media!["token"] as JsonArray)?.FirstOrDefault()?.GetValue<string>();
        var url = baseUri + path.Replace("<prettyName>", prettyName) + (token is null ? "" : $"?token={token}");
        return new PageImage(
            item!["position"]?.GetValue<int>() ?? int.MaxValue,
            url,
            fullview!["w"]?.GetValue<int>() ?? 0,
            fullview["h"]?.GetValue<int>() ?? 0,
            Blurred: path.Contains("blur_", StringComparison.Ordinal));
    }

    private static JsonNode? ReadInitialState(string html)
    {
        var start = html.IndexOf(StateMarker, StringComparison.Ordinal);
        if (start < 0)
            return null;

        var literal = new StringBuilder();
        for (var i = start + StateMarker.Length; i < html.Length; i++)
        {
            var current = html[i];
            if (current == '"')
                return ParseJsonLiteral(literal.ToString());
            if (current == '\\' && i + 1 < html.Length)
            {
                var escaped = html[++i];
                // JavaScript allows \' inside double-quoted strings; JSON does not.
                literal.Append(escaped == '\'' ? "'" : $"\\{escaped}");
            }
            else
            {
                literal.Append(current);
            }
        }
        return null;
    }

    private static JsonNode? ParseJsonLiteral(string literal)
    {
        try
        {
            var json = JsonNode.Parse($"\"{literal}\"")?.GetValue<string>();
            return json is null ? null : JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
