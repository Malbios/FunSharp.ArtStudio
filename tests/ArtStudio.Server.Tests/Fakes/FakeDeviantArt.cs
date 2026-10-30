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
    /// <summary>When set, oEmbed answers with this status, like NotFound for a deleted deviation.</summary>
    public HttpStatusCode? OEmbedFailure { get; set; }
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

    public const string PublishedUrl = "https://www.deviantart.com/studio-owner/art/Ember-Fox-1234567890";
    /// <summary>Each publication's form fields in the order sent, as "name=value".</summary>
    public List<List<string>> Publications { get; } = [];

    /// <summary>When set, every gallery, Stash and tag call answers with this instead.</summary>
    public (HttpStatusCode Status, string Body)? ApiFailure { get; set; }

    /// <summary>Tags that at least one deviation uses.</summary>
    public HashSet<string> KnownTags { get; } = [];
    public List<string> TagChecks { get; } = [];
    /// <summary>How many tag checks are answered with DeviantArt's rate limit before they succeed again.</summary>
    public int RateLimitedTagChecks { get; set; }

    public const string PublishedDeviationId = "DEV-1";
    /// <summary>The account's own deviations as gallery/all lists them, newest first.</summary>
    public List<(string DeviationId, string Url, DateTimeOffset PublishedTime)> OwnDeviations { get; } = [];
    public List<string> GalleryAllRequests { get; } = [];
    public Dictionary<string, (int Views, int Favourites, int Comments, int Downloads)> Stats { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> MetadataRequests { get; } = [];
    /// <summary>When set, metadata requests answer with this instead.</summary>
    public (HttpStatusCode Status, string Body)? MetadataFailure { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var uri = request.RequestUri!;
        if (uri.Host == "backend.deviantart.com" && uri.AbsolutePath == "/oembed")
        {
            OEmbedCalls++;
            var title = HttpUtility.ParseQueryString(uri.Query)["url"];
            if (OEmbedFailure is { } status)
                return new HttpResponseMessage(status);
            return FakeHttpHandler.Json($$"""
                { "type": "photo", "title": "{{title}}", "url": "{{ImageUrl}}", "author_name": "{{Author}}", "width": 1600, "height": 900,
                  "safety": "{{(Mature ? "adult" : "nonadult")}}" }
                """);
        }

        if (uri.AbsolutePath == "/oauth2/token")
            return Token(await request.Content!.ReadAsStringAsync(ct));

        if (uri.AbsolutePath == "/api/v1/oauth2/user/whoami")
            return FakeHttpHandler.Json($$"""{ "username": "{{Username}}" }""");

        if (uri.AbsolutePath.StartsWith("/api/v1/oauth2/gallery/") || uri.AbsolutePath.StartsWith("/api/v1/oauth2/stash/")
            || uri.AbsolutePath == "/api/v1/oauth2/browse/tags")
        {
            if (ApiFailure is { } failure)
                return FakeHttpHandler.Json(failure.Body, failure.Status);
            if (uri.AbsolutePath == "/api/v1/oauth2/browse/tags")
            {
                var tag = HttpUtility.ParseQueryString(uri.Query)["tag"]!;
                lock (TagChecks)
                {
                    TagChecks.Add(uri.PathAndQuery);
                    if (RateLimitedTagChecks > 0)
                    {
                        RateLimitedTagChecks--;
                        return FakeHttpHandler.Json("""{ "error": "rate_limited" }""", HttpStatusCode.TooManyRequests);
                    }
                }
                var results = KnownTags.Contains(tag) ? """[{ "deviationid": "DEV-T" }]""" : "[]";
                return FakeHttpHandler.Json($$"""{ "has_more": false, "results": {{results}} }""");
            }
            if (uri.AbsolutePath == "/api/v1/oauth2/gallery/folders")
                return GalleryPage(int.Parse(HttpUtility.ParseQueryString(uri.Query)["offset"] ?? "0"));
            if (uri.AbsolutePath == "/api/v1/oauth2/gallery/all")
                return GalleryAllPage(HttpUtility.ParseQueryString(uri.Query));
            if (uri.AbsolutePath == "/api/v1/oauth2/stash/submit")
                return await StashSubmit((MultipartFormDataContent)request.Content!, ct);
            if (uri.AbsolutePath == "/api/v1/oauth2/stash/publish")
            {
                var form = await request.Content!.ReadAsStringAsync(ct);
                lock (Publications)
                    Publications.Add(form.Split('&').Select(pair => Uri.UnescapeDataString(pair.Replace('+', ' '))).ToList());
                return FakeHttpHandler.Json($$"""
                    { "status": "success", "url": "{{PublishedUrl}}", "deviationid": "{{PublishedDeviationId}}" }
                    """);
            }
        }

        if (uri.AbsolutePath == "/api/v1/oauth2/deviation/metadata")
            return Metadata(uri);

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

    private HttpResponseMessage GalleryAllPage(System.Collections.Specialized.NameValueCollection query)
    {
        lock (GalleryAllRequests)
            GalleryAllRequests.Add(query.ToString()!);
        var offset = int.Parse(query["offset"] ?? "0");
        var page = OwnDeviations.Skip(offset).Take(int.Parse(query["limit"] ?? "10")).ToList();
        var hasMore = offset + page.Count < OwnDeviations.Count;
        var results = string.Join(',', page.Select(deviation =>
            $$"""{ "deviationid": "{{deviation.DeviationId}}", "url": "{{deviation.Url}}", "published_time": "{{deviation.PublishedTime.ToUnixTimeSeconds()}}" }"""));
        return FakeHttpHandler.Json($$"""
            { "results": [{{results}}], "has_more": {{(hasMore ? "true" : "false")}}, "next_offset": {{(hasMore ? offset + page.Count : "null")}} }
            """);
    }

    private HttpResponseMessage Metadata(Uri uri)
    {
        lock (MetadataRequests)
            MetadataRequests.Add(Uri.UnescapeDataString(uri.Query));
        if (MetadataFailure is { } failure)
            return FakeHttpHandler.Json(failure.Body, failure.Status);
        var ids = HttpUtility.ParseQueryString(uri.Query).GetValues("deviationids[]") ?? [];
        var metadata = string.Join(',', ids.Where(Stats.ContainsKey).Select(id =>
        {
            var stats = Stats[id];
            return $$"""
                { "deviationid": "{{id}}", "stats": { "views": {{stats.Views}}, "views_today": 0, "favourites": {{stats.Favourites}},
                  "comments": {{stats.Comments}}, "downloads": {{stats.Downloads}}, "downloads_today": 0 } }
                """;
        }));
        return FakeHttpHandler.Json($$"""{ "metadata": [{{metadata}}] }""");
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
