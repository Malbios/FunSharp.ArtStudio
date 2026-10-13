using System.Net;
using System.Text;

namespace ArtStudio.Server.DeviantArt;

/// <summary>
/// On test instances, answers publish requests itself so nothing is ever published, while every other DeviantArt
/// call (login, galleries, Stash uploads) still reaches the real DeviantArt.
/// </summary>
public sealed class FakeDeviantArtPublishHandler : DelegatingHandler
{
    public const string ConfigurationKey = "ArtStudio:FakeDeviantArtPublish";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (!request.RequestUri!.AbsolutePath.EndsWith("/stash/publish"))
            return base.SendAsync(request, ct);

        var deviationId = Guid.NewGuid().ToString().ToUpperInvariant();
        var body = $$"""
            { "status": "success", "url": "https://www.deviantart.com/artstudio-fake/art/Not-published-{{Random.Shared.Next(100000, 999999)}}", "deviationid": "{{deviationId}}" }
            """;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
    }
}
