using System.Net;
using System.Text;

namespace ArtStudio.Server.Tests.Fakes;

public sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<(HttpMethod Method, string PathAndQuery, string? Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        lock (Requests)
            Requests.Add((request.Method, request.RequestUri!.PathAndQuery, body));
        return respond(request);
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Bytes(byte[] content) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };

    public HttpClient CreateClient(string baseUrl = "http://comfy.test/") =>
        new(this) { BaseAddress = new Uri(baseUrl) };
}
