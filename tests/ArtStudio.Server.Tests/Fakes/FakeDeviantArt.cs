using System.Net;
using System.Net.Http.Headers;
using System.Web;

namespace ArtStudio.Server.Tests.Fakes;

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
