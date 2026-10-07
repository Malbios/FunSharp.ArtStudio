using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ArtStudio.Server.Data;
using ArtStudio.Server.Generation;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.DeviantArt;

public sealed class DuplicateDeviationException(int existingSetId)
    : UserFacingException($"This deviation was already used in set #{existingSetId}.")
{
    public int ExistingSetId { get; } = existingSetId;
}

public sealed record DeviationPreview(
    string Url, string DeviationId, string Author, string? Title, string ImageUrl, int Width, int Height);

public sealed record DownloadedDeviation(DeviationPreview Preview, byte[] Content, string Extension);

public sealed partial class DeviantArtService(StudioDbContext db, IHttpClientFactory httpClientFactory, DeviantArtAuth auth)
{
    public const string HttpClientName = "deviantart";
    public const string DeviationApiEndpoint = "https://www.deviantart.com/api/v1/oauth2/deviation";
    private const string OEmbedEndpoint = "https://backend.deviantart.com/oembed";

    [GeneratedRegex(@"DeviantArt://deviation/(?<uuid>[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})")]
    private static partial Regex DeviationUuid();

    public async Task<DeviationPreview> PreviewAsync(string input, CancellationToken ct)
    {
        var deviation = DeviationUrl.Parse(input);
        if (deviation.Username is not null)
            await EnsureNotBlockedAsync(deviation.Username, ct);
        await EnsureNotUsedAsync(deviation.DeviationId, ct);

        var preview = await auth.IsConnectedAsync(ct)
            ? await FetchFromApiAsync(deviation, ct)
            : await FetchFromOEmbedAsync(deviation, ct);
        await EnsureNotBlockedAsync(preview.Author, ct);
        return preview;
    }

    public async Task<DownloadedDeviation> DownloadAsync(string input, CancellationToken ct)
    {
        var preview = await PreviewAsync(input, ct);

        using var response = await Http().GetAsync(preview.ImageUrl, ct);
        if (!response.IsSuccessStatusCode)
            throw new UserFacingException($"Downloading the deviation image failed ({(int)response.StatusCode}).");

        var extension = ImageStore.ExtensionFor(response.Content.Headers.ContentType?.MediaType)
            ?? throw new UserFacingException("The deviation image has an unsupported format.");
        return new DownloadedDeviation(preview, await response.Content.ReadAsByteArrayAsync(ct), extension);
    }

    private async Task EnsureNotBlockedAsync(string username, CancellationToken ct)
    {
        if (await db.BlockedArtists.AnyAsync(a => a.Username == username, ct))
            throw new UserFacingException($"DeviantArt user '{username}' is on your block list.");
    }

    private async Task EnsureNotUsedAsync(string deviationId, CancellationToken ct)
    {
        var existingSetId = await db.PromptSets
            .Where(s => s.DeviationId == deviationId)
            .OrderBy(s => s.Id)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(ct);
        if (existingSetId is not null)
            throw new DuplicateDeviationException(existingSetId.Value);
    }

    // The API only accepts the deviation UUID, which the public page exposes in its da:appurl meta tag.
    private async Task<DeviationPreview> FetchFromApiAsync(DeviationUrl deviation, CancellationToken ct)
    {
        using var page = await Http().GetAsync(deviation.Url, ct);
        if (!page.IsSuccessStatusCode)
            throw new UserFacingException($"DeviantArt could not find that deviation ({(int)page.StatusCode}).");
        var uuid = DeviationUuid().Match(await page.Content.ReadAsStringAsync(ct)).Groups["uuid"].Value;
        if (uuid.Length == 0)
            throw new UserFacingException("Could not find the deviation's ID on its DeviantArt page.");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{DeviationApiEndpoint}/{uuid}?mature_content=true");
        request.Headers.Authorization = new("Bearer", await auth.GetAccessTokenAsync(ct));
        using var response = await Http().SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UserFacingException("DeviantArt rejected the login. Reconnect it in Settings.");
        if (!response.IsSuccessStatusCode)
            throw new UserFacingException($"DeviantArt could not load that deviation ({(int)response.StatusCode}).");

        var deviationJson = await ParseJsonAsync(response, ct);
        var content = deviationJson?["content"];
        var imageUrl = content?["src"]?.GetValue<string>();
        var width = ReadInt(content?["width"]);
        var height = ReadInt(content?["height"]);
        var author = deviationJson?["author"]?["username"]?.GetValue<string>();
        if (imageUrl is null || width is null || height is null || author is null)
            throw new UserFacingException("That deviation has no image that can be used.");

        return new DeviationPreview(
            deviation.Url, deviation.DeviationId, author, deviationJson?["title"]?.GetValue<string>(), imageUrl, width.Value, height.Value);
    }

    private async Task<DeviationPreview> FetchFromOEmbedAsync(DeviationUrl deviation, CancellationToken ct)
    {
        using var response = await Http().GetAsync($"{OEmbedEndpoint}?url={Uri.EscapeDataString(deviation.Url)}", ct);
        if (!response.IsSuccessStatusCode)
            throw new UserFacingException($"DeviantArt could not resolve that deviation ({(int)response.StatusCode}).");

        var oEmbed = await ParseJsonAsync(response, ct);
        if (oEmbed?["safety"]?.GetValue<string>() == "adult")
            throw new UserFacingException(
                "This deviation is marked mature, so DeviantArt only shows it blurred. Connect DeviantArt in Settings to use it.");

        var imageUrl = oEmbed?["url"]?.GetValue<string>();
        var author = oEmbed?["author_name"]?.GetValue<string>();
        var width = ReadInt(oEmbed?["width"]);
        var height = ReadInt(oEmbed?["height"]);
        if (oEmbed?["type"]?.GetValue<string>() != "photo" || imageUrl is null || author is null || width is null || height is null)
            throw new UserFacingException("That deviation has no image that can be used.");

        return new DeviationPreview(
            deviation.Url, deviation.DeviationId, author, oEmbed?["title"]?.GetValue<string>(), imageUrl, width.Value, height.Value);
    }

    private static async Task<JsonNode?> ParseJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
        }
        catch (JsonException)
        {
            throw new UserFacingException("DeviantArt returned an unexpected response.");
        }
    }

    private static int? ReadInt(JsonNode? node) => node?.GetValueKind() switch
    {
        JsonValueKind.Number => node.GetValue<int>(),
        JsonValueKind.String when int.TryParse(node.GetValue<string>(), out var value) => value,
        _ => null,
    };

    private HttpClient Http() => httpClientFactory.CreateClient(HttpClientName);
}
