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
    public async Task<string> FindFolderAsync(string path, CancellationToken ct) =>
        await TryFindFolderAsync(path, ct) ?? throw new UserFacingException($"The Google Drive folder '{path}' does not exist.");

    /// <summary>Like <see cref="FindFolderAsync"/>, but null while any folder of the path does not exist (yet).</summary>
    public async Task<string?> TryFindFolderAsync(string path, CancellationToken ct)
    {
        var parentId = "root";
        foreach (var name in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var query = $"name = '{EscapeQuery(name)}' and mimeType = '{FolderMimeType}' and '{parentId}' in parents and trashed = false";
            var found = await ListAsync(query, "files(id)", ct);
            if (found.Count == 0)
                return null;
            if (found.Count > 1)
                throw new UserFacingException($"Google Drive has {found.Count} folders named '{name}' (in '{path}').");
            parentId = found[0]["id"]!.GetValue<string>();
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

    /// <summary>Every file in the folder and its subfolders, at any depth; folders themselves are not listed.</summary>
    public async Task<IReadOnlyList<DriveFile>> ListFilesRecursivelyAsync(string folderId, CancellationToken ct)
    {
        var files = new List<DriveFile>();
        var folders = new Queue<string>([folderId]);
        while (folders.TryDequeue(out var current))
        {
            foreach (var item in await ListAllPagesAsync($"'{current}' in parents and trashed = false", ct))
            {
                var id = item["id"]!.GetValue<string>();
                if (item["mimeType"]?.GetValue<string>() == FolderMimeType)
                    folders.Enqueue(id);
                else
                    files.Add(new DriveFile(id, item["name"]!.GetValue<string>(), long.TryParse(item["size"]?.GetValue<string>(), out var size) ? size : 0));
            }
        }
        return files;
    }

    /// <summary>Moves a file to Drive's trash, where it stays restorable for 30 days.</summary>
    public async Task TrashAsync(DriveFile file, CancellationToken ct)
    {
        using var response = await SendAsync(
            HttpMethod.Patch, $"{ApiBase}/files/{Uri.EscapeDataString(file.Id)}", """{ "trashed": true }""", ct);
        await EnsureSuccessAsync(response, $"moving {file.Name} to the trash", ct);
    }

    private async Task<List<JsonNode>> ListAllPagesAsync(string query, CancellationToken ct)
    {
        var items = new List<JsonNode>();
        string? pageToken = null;
        do
        {
            var url = $"{ApiBase}/files?q={Uri.EscapeDataString(query)}&fields={Uri.EscapeDataString("nextPageToken, files(id, name, mimeType, size)")}&pageSize=1000"
                + (pageToken is null ? "" : $"&pageToken={Uri.EscapeDataString(pageToken)}");
            using var response = await SendAsync(HttpMethod.Get, url, jsonBody: null, ct);
            await EnsureSuccessAsync(response, "listing Google Drive", ct);
            var body = GoogleAuth.ParseJsonOrNull(await response.Content.ReadAsStringAsync(ct));
            items.AddRange((body?["files"] as JsonArray ?? []).OfType<JsonNode>());
            pageToken = body?["nextPageToken"]?.GetValue<string>();
        } while (pageToken is not null);
        return items;
    }

    public async Task DownloadAsync(DriveFile file, Stream destination, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{ApiBase}/files/{Uri.EscapeDataString(file.Id)}?alt=media", jsonBody: null, ct);
        await EnsureSuccessAsync(response, $"downloading {file.Name}", ct);
        await response.Content.CopyToAsync(destination, ct);
    }

    private async Task<List<JsonNode>> ListAsync(string query, string fields, CancellationToken ct)
    {
        var url = $"{ApiBase}/files?q={Uri.EscapeDataString(query)}&fields={Uri.EscapeDataString(fields)}&pageSize=100";
        using var response = await SendAsync(HttpMethod.Get, url, jsonBody: null, ct);
        await EnsureSuccessAsync(response, "searching Google Drive", ct);
        var body = GoogleAuth.ParseJsonOrNull(await response.Content.ReadAsStringAsync(ct));
        return (body?["files"] as JsonArray ?? []).OfType<JsonNode>().ToList();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? jsonBody, CancellationToken ct)
    {
        var response = await SendOnceAsync(method, url, jsonBody, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        // A cached access token can be revoked before it expires; one fresh token is worth a retry.
        response.Dispose();
        auth.ForgetAccessToken();
        return await SendOnceAsync(method, url, jsonBody, ct);
    }

    private async Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string url, string? jsonBody, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new("Bearer", await auth.GetAccessTokenAsync(ct));
        if (jsonBody is not null)
            request.Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
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
