using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ArtStudio.Server.Google;

/// <summary>
/// Stands in for Google's login and Drive on manual test instances, so they never reach a real Google account:
/// every folder exists, and files appear when the fake ComfyUI "renders" them.
/// </summary>
public sealed partial class FakeGoogleDriveHandler : HttpMessageHandler
{
    private sealed record FakeFile(string Id, string FolderId, string Name, byte[] Content)
    {
        public bool Trashed { get; set; }
    }

    private readonly ConcurrentDictionary<string, FakeFile> _files = new();

    [GeneratedRegex(@"name = '(?<name>(?:[^'\\]|\\.)*)'")]
    private static partial Regex NameCondition();

    [GeneratedRegex(@"'(?<parent>(?:[^'\\]|\\.)*)' in parents")]
    private static partial Regex ParentCondition();

    /// <summary>Puts a file into the folder at the given path from the root of My Drive.</summary>
    public void AddFile(string folderPath, string name, byte[] content)
    {
        var id = Guid.NewGuid().ToString("N");
        _files[id] = new FakeFile(id, FolderId(folderPath), name, content);
    }

    private static string FolderId(string path) =>
        string.Join('/', path.Split('/', StringSplitOptions.RemoveEmptyEntries).Prepend("root"));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var uri = request.RequestUri!;
        var path = uri.AbsolutePath;
        var response = (request.Method.Method, uri.Host, path) switch
        {
            ("POST", "oauth2.googleapis.com", "/token") => Json(new JsonObject
            {
                ["access_token"] = "fake-access-token",
                ["refresh_token"] = "fake-refresh-token",
                ["expires_in"] = 3600,
            }),
            ("GET", _, "/drive/v3/about") => Json(new JsonObject { ["user"] = new JsonObject { ["emailAddress"] = "test@example.com" } }),
            ("GET", _, "/drive/v3/files") => List(System.Web.HttpUtility.ParseQueryString(uri.Query)["q"] ?? ""),
            ("GET", _, var p) when p.StartsWith("/drive/v3/files/") => Download(p["/drive/v3/files/".Length..]),
            ("PATCH", _, var p) when p.StartsWith("/drive/v3/files/") => Trash(p["/drive/v3/files/".Length..]),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
        return Task.FromResult(response);
    }

    private HttpResponseMessage List(string query)
    {
        var parent = Unescape(ParentCondition().Match(query).Groups["parent"].Value);
        var name = NameCondition().Match(query) is { Success: true } match ? Unescape(match.Groups["name"].Value) : null;

        if (query.Contains(GoogleDrive.FolderMimeType))
            return Json(new JsonObject { ["files"] = new JsonArray(new JsonObject { ["id"] = $"{parent}/{name}" }) });

        var live = _files.Values.Where(f => !f.Trashed).ToList();
        var files = live
            .Where(f => f.FolderId == parent && (name is null || f.Name == name))
            .Select(f => (JsonNode)new JsonObject
            {
                ["id"] = f.Id,
                ["name"] = f.Name,
                ["mimeType"] = "video/mp4",
                ["size"] = f.Content.Length.ToString(),
                ["md5Checksum"] = Convert.ToHexStringLower(System.Security.Cryptography.MD5.HashData(f.Content)),
            });
        // Folders exist implicitly here: listing a folder shows the subfolders its files live in.
        var subfolders = name is not null
            ? []
            : live
                .Where(f => f.FolderId.StartsWith(parent + "/"))
                .Select(f => f.FolderId[(parent.Length + 1)..].Split('/')[0])
                .Distinct()
                .Select(folder => (JsonNode)new JsonObject
                {
                    ["id"] = $"{parent}/{folder}",
                    ["name"] = folder,
                    ["mimeType"] = GoogleDrive.FolderMimeType,
                });
        return Json(new JsonObject { ["files"] = new JsonArray([.. files, .. subfolders]) });
    }

    private HttpResponseMessage Trash(string id)
    {
        if (!_files.TryGetValue(id, out var file))
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        file.Trashed = true;
        return Json(new JsonObject { ["id"] = id });
    }

    private HttpResponseMessage Download(string id)
    {
        if (!_files.TryGetValue(id, out var file))
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(file.Content) };
    }

    private static string Unescape(string value) => value.Replace("\\'", "'").Replace("\\\\", "\\");

    private static HttpResponseMessage Json(JsonNode body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
}
