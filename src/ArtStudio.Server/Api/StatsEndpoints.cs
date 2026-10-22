using ArtStudio.Server.DeviantArt;
using ArtStudio.Server.Stats;

namespace ArtStudio.Server.Api;

public sealed record RefreshStatsRequest(bool Force);

public sealed record RefreshStatsDto(string? Error);

public static class StatsEndpoints
{
    public static void MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/stats", (StatsService stats, CancellationToken ct) => stats.GetAsync(ct));
        app.MapPost("/api/stats/deviantart/refresh", async (RefreshStatsRequest? request, DeviationStatsCollector collector, CancellationToken ct) =>
            new RefreshStatsDto(await collector.RefreshAsync(request?.Force ?? false, ct)));
    }
}
