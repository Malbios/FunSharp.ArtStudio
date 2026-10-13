using System.Net;
using System.Net.Http.Headers;
using System.Web;

namespace ArtStudio.Server.Tests.Fakes;

public sealed record StashPart(string Name, string? Value, byte[]? File, string? ContentType);

public sealed class FakeDeviantArt : HttpMessageHandler
{
    public const string ImageUrl = "https://images.example.test/deviation.jpg";
    public const string ApiImageUrl = "https://images.example.test/deviation-full.jpg";
    public const string DeviationUuid = "DAE6D14C-7B78-9BD8-9585-09157BC8A55A";
    public const string Username = "studio-owner";
    public static readonly byte[] ApiJpegBytes = [0xFF, 0xD8, 0xFF, 0xE1];
    public static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0];

    private int _tokenCounter;

    public string Author { get; set; } = "someartist";
    public bool Mature { get; set; }
    public int ExtraImages { get; set; }
    public int OEmbedCalls { get; private set; }
    public List<string> DeviationApiRequests { get; } = [];
    public int AccessTokenLifetimeSeconds { get; set; } = 3600;
    public bool RejectRefresh { get; set; }
    public List<Dictionary<string, string>> TokenRequests { get; } = [];

    /// <summary>Kept small so that listing galleries has to follow DeviantArt's paging.</summary>
    public const int GalleryPageSize = 2;
    public List<(string FolderId, string Name)> Galleries { get; } =
        [("F-FEATURED", "Featured"), ("F-SPICY", "Spicy"), ("F-RANDOM", "RandomPile")];

    /// <summary>Larger than JavaScript can hold exactly, like real Stash ids can be.</summary>
    public const long StashItemId = 9_123_456_789_012_345_678;
    public List<List<StashPart>> StashSubmissions { get; } = [];

    /// <summary>When set, every gallery and Stash call answers with this instead.</summary>
    public (HttpStatusCode Status, string Body)? ApiFailure { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var uri = request.RequestUri!;
        if (uri.Host == "backend.deviantart.com" && uri.AbsolutePath == "/oembed")
        {
            OEmbedCalls++;
            var title = HttpUtility.ParseQueryString(uri.Query)["url"];
            return FakeHttpHandler.Json($$"""
                { "type": "photo", "title": "{{title}}", "url": "{{ImageUrl}}", "author_name": "{{Author}}", "width": 1600, "height": 900,
                  "safety": "{{(Mature ? "adult" : "nonadult")}}" }
                """);
        }

        if (uri.AbsolutePath == "/oauth2/token")
            return Token(await request.Content!.ReadAsStringAsync(ct));

        if (uri.AbsolutePath == "/api/v1/oauth2/user/whoami")
            return FakeHttpHandler.Json($$"""{ "username": "{{Username}}" }""");

        if (uri.AbsolutePath.StartsWith("/api/v1/oauth2/gallery/") || uri.AbsolutePath.StartsWith("/api/v1/oauth2/stash/"))
        {
            if (ApiFailure is { } failure)
                return FakeHttpHandler.Json(failure.Body, failure.Status);
            if (uri.AbsolutePath == "/api/v1/oauth2/gallery/folders")
                return GalleryPage(int.Parse(HttpUtility.ParseQueryString(uri.Query)["offset"] ?? "0"));
            if (uri.AbsolutePath == "/api/v1/oauth2/stash/submit")
                return await StashSubmit((MultipartFormDataContent)request.Content!, ct);
        }

        if (uri.AbsolutePath.StartsWith("/api/v1/oauth2/deviation/"))
        {
            lock (DeviationApiRequests)
                DeviationApiRequests.Add($"{request.Headers.Authorization} {uri.PathAndQuery}");
            return FakeHttpHandler.Json($$"""
                { "title": "Full deviation", "is_mature": {{(Mature ? "true" : "false")}}, "author": { "username": "{{Author}}" },
                  "content": { "src": "{{ApiImageUrl}}", "width": 3000, "height": 2000 } }
                """);
        }

        if (uri.Host == "www.deviantart.com" && uri.AbsolutePath.Contains("/art/"))
        {
            var deviationNumber = uri.AbsolutePath.Split('-').Last();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(DeviationPageFixture.Html(DeviationUuid, deviationNumber, ExtraImages, blurred: Mature)),
            };
        }

        for (var position = 1; position <= ExtraImages; position++)
        {
            if (uri.ToString() == DeviationPageFixture.ExtraImageUrl(position))
                return Image(DeviationPageFixture.ExtraImageBytes(position));
        }

        if (uri.ToString() == ImageUrl)
            return Image(JpegBytes);
        if (uri.ToString() == ApiImageUrl)
            return Image(ApiJpegBytes);

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private async Task<HttpResponseMessage> StashSubmit(MultipartFormDataContent form, CancellationToken ct)
    {
        var parts = new List<StashPart>();
        foreach (var part in form)
        {
            var name = part.Headers.ContentDisposition!.Name!.Trim('"');
            parts.Add(part.Headers.ContentDisposition.FileName is null
                ? new StashPart(name, await part.ReadAsStringAsync(ct), null, null)
                : new StashPart(name, null, await part.ReadAsByteArrayAsync(ct), part.Headers.ContentType?.MediaType));
        }
        lock (StashSubmissions)
            StashSubmissions.Add(parts);
        return FakeHttpHandler.Json($$"""
            { "status": "success", "itemid": {{StashItemId}}, "stack": "Sta.sh", "stackid": 3307645702784122 }
            """);
    }

    private HttpResponseMessage GalleryPage(int offset)
    {
        var page = Galleries.Skip(offset).Take(GalleryPageSize).ToList();
        var hasMore = offset + page.Count < Galleries.Count;
        var results = string.Join(',', page.Select(gallery =>
            $$"""{ "folderid": "{{gallery.FolderId}}", "parent": null, "name": "{{gallery.Name}}", "description": "", "thumb": null }"""));
        return FakeHttpHandler.Json($$"""
            { "results": [{{results}}], "has_more": {{(hasMore ? "true" : "false")}}, "next_offset": {{(hasMore ? offset + page.Count : "null")}} }
            """);
    }

    private static HttpResponseMessage Image(byte[] bytes)
    {
        var image = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        image.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return image;
    }

    private HttpResponseMessage Token(string form)
    {
        var grant = HttpUtility.ParseQueryString(form);
        lock (TokenRequests)
            TokenRequests.Add(grant.AllKeys.ToDictionary(key => key!, key => grant[key]!));

        if (grant["grant_type"] == "refresh_token" && RejectRefresh)
            return FakeHttpHandler.Json("""{ "error": "invalid_grant", "error_description": "Refresh token expired" }""", HttpStatusCode.BadRequest);

        var number = Interlocked.Increment(ref _tokenCounter);
        return FakeHttpHandler.Json($$"""
            { "status": "success", "access_token": "access-{{number}}", "refresh_token": "refresh-{{number}}", "expires_in": {{AccessTokenLifetimeSeconds}} }
            """);
    }
}
