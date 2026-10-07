using System.Net;
using System.Net.Http.Headers;
using System.Web;

namespace ArtStudio.Server.Tests.Fakes;

public sealed class FakeDeviantArt : HttpMessageHandler
{
    public const string ImageUrl = "https://images.example.test/deviation.jpg";
    public const string Username = "studio-owner";
    public static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0];

    private int _tokenCounter;

    public string Author { get; set; } = "someartist";
    public int OEmbedCalls { get; private set; }
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
                { "type": "photo", "title": "{{title}}", "url": "{{ImageUrl}}", "author_name": "{{Author}}", "width": 1600, "height": 900 }
                """);
        }

        if (uri.AbsolutePath == "/oauth2/token")
            return Token(await request.Content!.ReadAsStringAsync(ct));

        if (uri.AbsolutePath == "/api/v1/oauth2/user/whoami")
            return FakeHttpHandler.Json($$"""{ "username": "{{Username}}" }""");

        if (uri.ToString() == ImageUrl)
        {
            var image = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(JpegBytes) };
            image.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            return image;
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
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
