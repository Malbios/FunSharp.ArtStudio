using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArtStudio.Server.Data;
using ArtStudio.Server.Generation;

namespace ArtStudio.Server.DeviantArt;

public sealed record Gallery(string FolderId, string Name, bool Mature);

public sealed class DeviantArtPosting(
    StudioDbContext db, AppPaths paths, IHttpClientFactory httpClientFactory, DeviantArtAuth auth)
{
    public const string ApiBase = "https://www.deviantart.com/api/v1/oauth2";
    private const int GalleryPageSize = 50;
    private const char IdSeparator = ',';

    public async Task<IReadOnlyList<Gallery>> GalleriesAsync(CancellationToken ct)
    {
        var matureIds = await MatureGalleryIdsAsync(ct);
        var galleries = new List<Gallery>();
        int? offset = 0;
        while (offset is not null)
        {
            var page = await SendAsync(HttpMethod.Get, $"gallery/folders?limit={GalleryPageSize}&offset={offset}", null, ct);
            foreach (var folder in page["results"]?.AsArray() ?? [])
            {
                var folderId = folder?["folderid"]?.GetValue<string>();
                var name = folder?["name"]?.GetValue<string>();
                if (folderId is not null && name is not null)
                    galleries.Add(new Gallery(folderId, name, matureIds.Contains(folderId)));
            }
            offset = page["has_more"]?.GetValue<bool>() == true ? page["next_offset"]?.GetValue<int>() : null;
        }
        return galleries;
    }

    public async Task SaveMatureGalleriesAsync(IReadOnlyList<string> folderIds, CancellationToken ct)
    {
        var settings = await SettingsStore.LoadAsync(db, paths, ct);
        var ids = folderIds.Select(id => id.Trim()).Where(id => id.Length > 0).Distinct().ToList();
        settings.MatureGalleryIds = ids.Count == 0 ? null : string.Join(IdSeparator, ids);
        await db.SaveChangesAsync(ct);
    }

    private async Task<HashSet<string>> MatureGalleryIdsAsync(CancellationToken ct) =>
        (await SettingsStore.LoadAsync(db, paths, ct)).MatureGalleryIds?.Split(IdSeparator).ToHashSet() ?? [];

    private async Task<JsonNode> SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, $"{ApiBase}/{path}") { Content = content };
        request.Headers.Authorization = new("Bearer", await auth.GetAccessTokenAsync(ct));
        using var response = await httpClientFactory.CreateClient(DeviantArtService.HttpClientName).SendAsync(request, ct);
        var body = ParseJsonOrNull(await response.Content.ReadAsStringAsync(ct));

        if (response.IsSuccessStatusCode && body is not null)
            return body;
        throw new UserFacingException(ErrorMessage(response.StatusCode, body));
    }

    private static string ErrorMessage(HttpStatusCode status, JsonNode? body)
    {
        var error = body?["error"]?.GetValue<string>();
        var description = body?["error_description"]?.GetValue<string>();
        return status switch
        {
            HttpStatusCode.Unauthorized => "DeviantArt rejected the login. Reconnect it in Settings.",
            HttpStatusCode.Forbidden when error == "insufficient_scope" || description?.Contains("scope") == true =>
                "The DeviantArt login does not allow Stash and publishing yet. Reconnect DeviantArt in Settings.",
            HttpStatusCode.TooManyRequests => "DeviantArt is limiting requests. Try again in a minute.",
            _ when description is not null => $"DeviantArt refused: {description}",
            _ when body is null => $"DeviantArt returned an unreadable response ({(int)status}).",
            _ => $"DeviantArt refused the request ({(int)status}).",
        };
    }

    private static JsonNode? ParseJsonOrNull(string text)
    {
        try
        {
            return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
