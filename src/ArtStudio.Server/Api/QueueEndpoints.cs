using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Generation;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.Api;

public sealed record MoveJobRequest(int Offset);

public static class QueueEndpoints
{
    public static void MapQueueEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/queue", GetQueueAsync);
        app.MapPost("/api/queue/pause", (QueueService queue, CancellationToken ct) => queue.SetPausedAsync(true, ct));
        app.MapPost("/api/queue/resume", (QueueService queue, CancellationToken ct) => queue.SetPausedAsync(false, ct));
        app.MapPost("/api/jobs/{id:int}/cancel", (int id, QueueService queue, CancellationToken ct) => queue.CancelAsync(id, ct));
        app.MapPost("/api/jobs/{id:int}/retry", (int id, QueueService queue, CancellationToken ct) => queue.RetryAsync(id, ct));
        app.MapPost("/api/jobs/{id:int}/move", (int id, MoveJobRequest request, QueueService queue, CancellationToken ct) =>
            queue.MoveAsync(id, request.Offset, ct));
    }

    private static async Task<QueueDto> GetQueueAsync(StudioDbContext db, AppPaths paths, CancellationToken ct)
    {
        var settings = await SettingsStore.LoadAsync(db, paths, ct);
        var activeStatuses = new[] { JobStatus.Running, JobStatus.Queued, JobStatus.Failed };

        var active = await db.Jobs
            .Include(j => j.PromptSet)
            .Where(j => activeStatuses.Contains(j.Status))
            .ToListAsync(ct);
        var ordered = active
            .OrderBy(j => j.Status switch { JobStatus.Running => 0, JobStatus.Failed => 1, _ => 2 })
            .ThenBy(j => j.QueuePosition)
            .Select(JobDto.From)
            .ToList();

        return new QueueDto(settings.QueuePaused, ordered);
    }
}
