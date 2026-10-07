using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArtStudio.Server.Data;
using ArtStudio.Server.Generation;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.DeviantArt;

public sealed class DuplicateDeviationException(int existingSetId)
    : UserFacingException($"This deviation was already used in set #{existingSetId}.")
{
    public int ExistingSetId { get; } = existingSetId;
}

public sealed record DeviationImage(int Index, string ImageUrl, int Width, int Height);

public sealed record DeviationPreview(
    string Url,
    string DeviationId,
    string Author,
    string? Title,
    IReadOnlyList<DeviationImage> Images,
    int UnavailableImageCount);

public sealed record DownloadedDeviation(DeviationPreview Preview, byte[] Content, string Extension);

public sealed class DeviantArtService(
    StudioDbContext db, IHttpClientFactory httpClientFactory, DeviantArtAuth auth, ILogger<DeviantArtService> logger)
{
    public const string HttpClientName = "deviantart";
    public const string DeviationApiEndpoint = "https://www.deviantart.com/api/v1/oauth2/deviation";
    private const string OEmbedEndpoint = "https://backend.deviantart.com/oembed";

    private sealed record MainImage(string Author, string? Title, string ImageUrl, int Width, int Height);

    public async Task<DeviationPreview> PreviewAsync(string input, CancellationToken ct)
    {
        var deviation = DeviationUrl.Parse(input);
        if (deviation.Username is not null)
            await EnsureNotBlockedAsync(deviation.Username, ct);
        await EnsureNotUsedAsync(deviation.DeviationId, ct);

        var connected = await auth.IsConnectedAsync(ct);
        var page = await FetchPageAsync(deviation, required: connected, ct);
        var main = connected
            ? await FetchFromApiAsync(page.Uuid!, ct)
            : await FetchFromOEmbedAsync(deviation, ct);
        await EnsureNotBlockedAsync(main.Author, ct);

        var usableExtras = page.AdditionalImages.Where(image => !image.Blurred).ToList();
        var images = usableExtras
            .Select((image, position) => new DeviationImage(position + 1, image.FullviewUrl, image.Width, image.Height))
            .Prepend(new DeviationImage(0, main.ImageUrl, main.Width, main.Height))
            .ToList();
        return new DeviationPreview(
            deviation.Url, deviation.DeviationId, main.Author, main.Title, images,
            page.AdditionalImages.Count - usableExtras.Count);
    }

    public async Task<DownloadedDeviation> DownloadAsync(string input, int imageIndex, CancellationToken ct)
    {
        var preview = await PreviewAsync(input, ct);
        var image = preview.Images.FirstOrDefault(i => i.Index == imageIndex)
            ?? throw new UserFacingException($"That DeviantArt post has no image #{imageIndex + 1}.");

        using var response = await Http().GetAsync(image.ImageUrl, ct);
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

    // The page supplies what the API lacks: the UUID the API needs, and the extra images of multi-image posts.
    private async Task<DeviationPage> FetchPageAsync(DeviationUrl deviation, bool required, CancellationToken ct)
    {
        using var response = await Http().GetAsync(deviation.Url, ct);
        if (!response.IsSuccessStatusCode)
        {
            if (required)
                throw new UserFacingException($"DeviantArt could not find that deviation ({(int)response.StatusCode}).");
            logger.LogWarning("Could not load DeviantArt page {Url} ({Status})", deviation.Url, (int)response.StatusCode);
            return new DeviationPage(null, [], StateParsed: false);
        }

        var page = DeviationPage.Parse(await response.Content.ReadAsStringAsync(ct), deviation.DeviationId);
        if (!page.StateParsed)
            logger.LogWarning("DeviantArt page format not recognized for {Url}; only the main image is offered", deviation.Url);
        if (required && page.Uuid is null)
            throw new UserFacingException("Could not find the deviation's ID on its DeviantArt page.");
        return page;
    }

    private async Task<MainImage> FetchFromApiAsync(string uuid, CancellationToken ct)
    {
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

        return new MainImage(author, deviationJson?["title"]?.GetValue<string>(), imageUrl, width.Value, height.Value);
    }

    private async Task<MainImage> FetchFromOEmbedAsync(DeviationUrl deviation, CancellationToken ct)
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

        return new MainImage(author, oEmbed?["title"]?.GetValue<string>(), imageUrl, width.Value, height.Value);
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
