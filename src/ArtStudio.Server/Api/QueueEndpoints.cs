using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Generation;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.Api;

public sealed record MoveJobRequest(QueueMove Move);

public static class QueueEndpoints
{
    public static void MapQueueEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/queue", GetQueueAsync);
        app.MapPost("/api/queue/pause", (JobKind? kind, QueueService queue, CancellationToken ct) => queue.SetPausedAsync(kind, true, ct));
        app.MapPost("/api/queue/resume", (JobKind? kind, QueueService queue, CancellationToken ct) => queue.SetPausedAsync(kind, false, ct));
        app.MapPost("/api/jobs/{id:int}/cancel", (int id, QueueService queue, CancellationToken ct) => queue.CancelAsync(id, ct));
        app.MapPost("/api/jobs/{id:int}/retry", (int id, QueueService queue, CancellationToken ct) => queue.RetryAsync(id, ct));
        app.MapPost("/api/jobs/{id:int}/move", (int id, MoveJobRequest request, QueueService queue, CancellationToken ct) =>
            queue.MoveAsync(id, request.Move, ct));
    }

    private static async Task<QueueDto> GetQueueAsync(
        StudioDbContext db, AppPaths paths, GenerationQueue queue, VideoTimeEstimator estimator, CancellationToken ct)
    {
        var secondsPerVideoUnit = await estimator.SecondsPerUnitAsync(ct);
        var settings = await SettingsStore.LoadAsync(db, paths, ct);
        var activeStatuses = new[] { JobStatus.Running, JobStatus.Queued, JobStatus.Failed };

        var active = await db.Jobs
            .Include(j => j.PromptSet)
            .Where(j => activeStatuses.Contains(j.Status))
            .ToListAsync(ct);
        var started = active
            .Where(j => j.Status != JobStatus.Queued)
            .OrderBy(j => j.Status == JobStatus.Running ? 0 : 1)
            .ThenBy(j => j.QueuePosition);
        var waiting = queue.InRunOrder(active.Where(j => j.Status == JobStatus.Queued), j => j.Kind, j => j.QueuePosition);

        return new QueueDto(
            settings.ImageQueuePaused, settings.VideoQueuePaused, queue.LoadedKind, started.Concat(waiting).Select(job => JobDto.From(job, secondsPerVideoUnit)).ToList());
    }
}
