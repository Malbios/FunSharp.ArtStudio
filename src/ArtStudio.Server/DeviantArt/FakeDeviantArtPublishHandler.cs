using System.Net;
using System.Text;
using System.Web;

namespace ArtStudio.Server.DeviantArt;

/// <summary>
/// On test instances, answers publish requests itself so nothing is ever published, and makes up the stats of the
/// deviations it pretended to publish, while every other DeviantArt call (login, galleries, Stash uploads) still
/// reaches the real DeviantArt.
/// </summary>
public sealed class FakeDeviantArtPublishHandler : DelegatingHandler
{
    public const string ConfigurationKey = "ArtStudio:FakeDeviantArtPublish";
    private static readonly DateTimeOffset StatsStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path.EndsWith("/stash/publish"))
            return Json($$"""
                { "status": "success", "url": "https://www.deviantart.com/artstudio-fake/art/Not-published-{{Random.Shared.Next(100000, 999999)}}", "deviationid": "{{Guid.NewGuid().ToString().ToUpperInvariant()}}" }
                """);
        if (path.EndsWith("/gallery/all"))
            return Json("""{ "has_more": false, "next_offset": null, "results": [] }""");
        if (path.EndsWith("/deviation/metadata"))
        {
            var deviationIds = HttpUtility.ParseQueryString(request.RequestUri.Query).GetValues("deviationids[]") ?? [];
            return Json($$"""{ "metadata": [{{string.Join(',', deviationIds.Select(MadeUpMetadata))}}] }""");
        }
        return base.SendAsync(request, ct);
    }

    private static string MadeUpMetadata(string deviationId)
    {
        var seed = deviationId.Sum(c => c) % 97;
        var views = seed * 3 + (int)((DateTimeOffset.UtcNow - StatsStart).TotalHours * (1 + seed % 4) / 20);
        return $$"""
            { "deviationid": "{{deviationId}}", "stats": { "views": {{views}}, "views_today": {{seed}}, "favourites": {{views / 12}},
              "comments": {{views / 40}}, "downloads": {{views / 25}}, "downloads_today": 0 } }
            """;
    }

    private static Task<HttpResponseMessage> Json(string body) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
}
