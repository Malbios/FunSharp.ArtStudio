using System.Net;
using System.Net.Http.Headers;
using System.Web;

namespace ArtStudio.Server.Tests.Fakes;

public sealed class FakeDeviantArt : HttpMessageHandler
{
    public const string ImageUrl = "https://images.example.test/deviation.jpg";
    public static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0];

    public string Author { get; set; } = "someartist";
    public int OEmbedCalls { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var uri = request.RequestUri!;
        if (uri.Host == "backend.deviantart.com" && uri.AbsolutePath == "/oembed")
        {
            OEmbedCalls++;
            var title = HttpUtility.ParseQueryString(uri.Query)["url"];
            return Task.FromResult(FakeHttpHandler.Json($$"""
                { "type": "photo", "title": "{{title}}", "url": "{{ImageUrl}}", "author_name": "{{Author}}", "width": 1600, "height": 900 }
                """));
        }

        if (uri.ToString() == ImageUrl)
        {
            var image = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(JpegBytes) };
            image.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            return Task.FromResult(image);
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
