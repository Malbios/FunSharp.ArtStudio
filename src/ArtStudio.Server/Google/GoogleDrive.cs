using System.Net;
using System.Text.Json.Nodes;
using ArtStudio.Server.Generation;

namespace ArtStudio.Server.Google;

public sealed record DriveFile(string Id, string Name, long Size);

public sealed class GoogleDrive(GoogleAuth auth, IHttpClientFactory httpClientFactory)
{
    public const string ApiBase = "https://www.googleapis.com/drive/v3";
    public const string FolderMimeType = "application/vnd.google-apps.folder";

    /// <summary>The id of a folder given as a path from the root of My Drive, e.g. "ComfyUI/output/video".</summary>
    public async Task<string> FindFolderAsync(string path, CancellationToken ct)
    {
        var parentId = "root";
        foreach (var name in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var query = $"name = '{EscapeQuery(name)}' and mimeType = '{FolderMimeType}' and '{parentId}' in parents and trashed = false";
            var found = await ListAsync(query, "files(id)", ct);
            parentId = found.Count switch
            {
                1 => found[0]["id"]!.GetValue<string>(),
                0 => throw new UserFacingException($"The Google Drive folder '{name}' (in '{path}') does not exist."),
                _ => throw new UserFacingException($"Google Drive has {found.Count} folders named '{name}' (in '{path}')."),
            };
        }
        return parentId;
    }

    /// <summary>The named file in the folder once it has fully arrived: Drive reports a checksum only for complete files.</summary>
    public async Task<DriveFile?> FindCompleteFileAsync(string folderId, string name, CancellationToken ct)
    {
        var query = $"name = '{EscapeQuery(name)}' and '{folderId}' in parents and trashed = false";
        var file = (await ListAsync(query, "files(id, name, size, md5Checksum)", ct))
            .FirstOrDefault(f => f["md5Checksum"] is not null && f["size"] is not null);
        return file is null
            ? null
            : new DriveFile(file["id"]!.GetValue<string>(), file["name"]!.GetValue<string>(), long.Parse(file["size"]!.GetValue<string>()));
    }

    public async Task DownloadAsync(DriveFile file, Stream destination, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{ApiBase}/files/{Uri.EscapeDataString(file.Id)}?alt=media", ct);
        await EnsureSuccessAsync(response, $"downloading {file.Name}", ct);
        await response.Content.CopyToAsync(destination, ct);
    }

    private async Task<List<JsonNode>> ListAsync(string query, string fields, CancellationToken ct)
    {
        var url = $"{ApiBase}/files?q={Uri.EscapeDataString(query)}&fields={Uri.EscapeDataString(fields)}&pageSize=100";
        using var response = await SendAsync(HttpMethod.Get, url, ct);
        await EnsureSuccessAsync(response, "searching Google Drive", ct);
        var body = GoogleAuth.ParseJsonOrNull(await response.Content.ReadAsStringAsync(ct));
        return (body?["files"] as JsonArray ?? []).OfType<JsonNode>().ToList();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, CancellationToken ct)
    {
        var response = await SendOnceAsync(method, url, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        // A cached access token can be revoked before it expires; one fresh token is worth a retry.
        response.Dispose();
        auth.ForgetAccessToken();
        return await SendOnceAsync(method, url, ct);
    }

    private async Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new("Bearer", await auth.GetAccessTokenAsync(ct));
        return await Http().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string action, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;
        var body = GoogleAuth.ParseJsonOrNull(await response.Content.ReadAsStringAsync(ct));
        var reason = body?["error"]?["message"]?.GetValue<string>() ?? $"status {(int)response.StatusCode}";
        throw new UserFacingException(response.StatusCode == HttpStatusCode.Unauthorized
            ? $"Google Drive refused access while {action}. Reconnect it in Settings."
            : $"Google Drive failed while {action}: {reason}");
    }

    private static string EscapeQuery(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");

    private HttpClient Http() => httpClientFactory.CreateClient(GoogleAuth.HttpClientName);
}
