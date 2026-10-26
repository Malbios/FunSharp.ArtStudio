using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;

namespace ArtStudio.Server.Tests.Fakes;

/// <summary>Google's token endpoint and a small in-memory Drive with folders and files.</summary>
public sealed partial class FakeGoogle : HttpMessageHandler
{
    public const string Account = "owner@example.com";
    public const string FolderMimeType = "application/vnd.google-apps.folder";

    public sealed class DriveItem(string id, string parentId, string name, bool isFolder, byte[] content)
    {
        public string Id { get; } = id;
        public string ParentId { get; } = parentId;
        public string Name { get; } = name;
        public bool IsFolder { get; } = isFolder;
        public byte[] Content { get; } = content;
        public bool Trashed { get; set; }
    }

    private readonly Lock _lock = new();
    private readonly List<DriveItem> _items = [];
    private int _tokenCounter;

    public int AccessTokenLifetimeSeconds { get; set; } = 3600;
    public bool RejectRefresh { get; set; }
    public List<Dictionary<string, string>> TokenRequests { get; } = [];
    public List<string> DownloadedFileIds { get; } = [];
    /// <summary>When set, every Drive call (not the token endpoint) answers with this status.</summary>
    public HttpStatusCode? DriveFailure { get; set; }

    [GeneratedRegex(@"name = '(?<name>(?:[^'\\]|\\.)*)'")]
    private static partial Regex NameCondition();

    [GeneratedRegex(@"'(?<parent>(?:[^'\\]|\\.)*)' in parents")]
    private static partial Regex ParentCondition();

    public IReadOnlyList<DriveItem> Items
    {
        get
        {
            lock (_lock)
                return _items.ToList();
        }
    }

    /// <summary>Creates the folders of the path from the root of My Drive as needed and returns the last one's id.</summary>
    public string AddFolder(string path)
    {
        lock (_lock)
        {
            var parentId = "root";
            foreach (var name in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                var existing = _items.FirstOrDefault(i => i.IsFolder && i.ParentId == parentId && i.Name == name);
                if (existing is null)
                {
                    existing = new DriveItem(Guid.NewGuid().ToString("N"), parentId, name, isFolder: true, []);
                    _items.Add(existing);
                }
                parentId = existing.Id;
            }
            return parentId;
        }
    }

    public DriveItem AddFile(string folderPath, string name, byte[] content)
    {
        var folderId = AddFolder(folderPath);
        lock (_lock)
        {
            var file = new DriveItem(Guid.NewGuid().ToString("N"), folderId, name, isFolder: false, content);
            _items.Add(file);
            return file;
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var uri = request.RequestUri!;
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        if (uri.Host == "oauth2.googleapis.com")
            return Token(body);
        if (DriveFailure is { } failure)
            return Json(new JsonObject { ["error"] = new JsonObject { ["message"] = "Drive is unavailable" } }, failure);

        var path = uri.AbsolutePath;
        return (request.Method.Method, path) switch
        {
            ("GET", "/drive/v3/about") => Json(new JsonObject { ["user"] = new JsonObject { ["emailAddress"] = Account } }),
            ("GET", "/drive/v3/files") => List(HttpUtility.ParseQueryString(uri.Query)["q"] ?? ""),
            ("GET", var p) when p.StartsWith("/drive/v3/files/") => Download(p["/drive/v3/files/".Length..]),
            ("PATCH", var p) when p.StartsWith("/drive/v3/files/") => Update(p["/drive/v3/files/".Length..], body),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
    }

    private HttpResponseMessage Token(string body)
    {
        var form = HttpUtility.ParseQueryString(body);
        var fields = form.AllKeys.ToDictionary(key => key!, key => form[key]!);
        lock (_lock)
            TokenRequests.Add(fields);

        if (fields["grant_type"] == "refresh_token" && RejectRefresh)
            return Json(new JsonObject { ["error"] = "invalid_grant", ["error_description"] = "Token has been expired or revoked." },
                HttpStatusCode.BadRequest);

        var answer = new JsonObject
        {
            ["access_token"] = $"access-{Interlocked.Increment(ref _tokenCounter)}",
            ["expires_in"] = AccessTokenLifetimeSeconds,
        };
        // Google only hands out a refresh token for the code exchange, never when refreshing.
        if (fields["grant_type"] == "authorization_code")
            answer["refresh_token"] = "refresh-1";
        return Json(answer);
    }

    private HttpResponseMessage List(string query)
    {
        var parent = Unescape(ParentCondition().Match(query).Groups["parent"].Value);
        var name = NameCondition().Match(query) is { Success: true } match ? Unescape(match.Groups["name"].Value) : null;
        var foldersOnly = query.Contains($"mimeType = '{FolderMimeType}'");

        var found = Items
            .Where(i => !i.Trashed && i.ParentId == parent && (name is null || i.Name == name) && (!foldersOnly || i.IsFolder))
            .Select(i => (JsonNode)new JsonObject
            {
                ["id"] = i.Id,
                ["name"] = i.Name,
                ["mimeType"] = i.IsFolder ? FolderMimeType : "video/mp4",
                ["size"] = i.IsFolder ? null : i.Content.Length.ToString(),
                ["md5Checksum"] = i.IsFolder ? null : Convert.ToHexStringLower(System.Security.Cryptography.MD5.HashData(i.Content)),
            });
        return Json(new JsonObject { ["files"] = new JsonArray(found.ToArray()) });
    }

    private HttpResponseMessage Download(string idAndQuery)
    {
        var item = Items.FirstOrDefault(i => i.Id == idAndQuery && !i.IsFolder);
        if (item is null)
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        lock (_lock)
            DownloadedFileIds.Add(item.Id);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(item.Content) };
    }

    private HttpResponseMessage Update(string id, string body)
    {
        var item = Items.FirstOrDefault(i => i.Id == id);
        if (item is null)
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        if (JsonNode.Parse(body)?["trashed"]?.GetValue<bool>() == true)
            item.Trashed = true;
        return Json(new JsonObject { ["id"] = item.Id });
    }

    private static string Unescape(string value) => value.Replace("\\'", "'").Replace("\\\\", "\\");

    private static HttpResponseMessage Json(JsonNode body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
}
