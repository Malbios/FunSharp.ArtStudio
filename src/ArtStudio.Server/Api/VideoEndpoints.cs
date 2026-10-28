using ArtStudio.Server.Comfy;
using ArtStudio.Server.Data;
using ArtStudio.Server.Generation;
using ArtStudio.Server.Hubs;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.Api;

public static class VideoEndpoints
{
    public static void MapVideoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/videos/options", (VideoWorkflowBuilder workflow) => new VideoOptionsDto(
            workflow.PromptTemplate,
            VideoOptions.DurationPresets,
            VideoOptions.ResolutionPresets.Select(p => new VideoResolutionPresetDto(p.Name, p.Megapixels, p.Note)).ToList(),
            VideoOptions.DefaultSeconds,
            VideoOptions.DefaultResolution,
            VideoOptions.MaxSeconds,
            VideoOptions.MaxMegapixels,
            VideoOptions.MaxVideosPerJob));
        app.MapGet("/api/videos/{id:int}", async (int id, StudioDbContext db, CancellationToken ct) =>
            SetEndpoints.FileResult(await db.Videos.Where(v => v.Id == id).Select(v => v.FilePath).SingleOrDefaultAsync(ct)));
        app.MapPut("/api/videos/{id:int}/pick", PickVideoAsync);
        app.MapPost("/api/videos/{id:int}/viewed", MarkVideoViewedAsync);
        app.MapGet("/api/jobs/{id:int}/frames/{frame}", GetFrameAsync);
        app.MapGet("/api/sets/{id:int}/video-frames", async (int id, StudioDbContext db, CancellationToken ct) =>
            (await FramesOfAsync(id, db, ct)).All.Select(f => new VideoFrameDto(f.Key, f.Url, f.Label)).ToList());
        app.MapPost("/api/sets/{id:int}/videos", MakeVideoAsync);
    }

    internal static async Task<VideoFrames> FramesOfAsync(int setId, StudioDbContext db, CancellationToken ct)
    {
        var set = await db.PromptSets
            .Include(s => s.Images)
            .Include(s => s.Jobs)
            .AsSplitQuery()
            .SingleOrDefaultAsync(s => s.Id == setId, ct)
            ?? throw new UserFacingException("Set not found.");
        return new VideoFrames(set, set.Images, set.Jobs);
    }

    /// <summary>Unlike image picks, allowed in every stage: videos are not part of the post.</summary>
    private static async Task<IResult> PickVideoAsync(
        int id, PickVideoRequest request, StudioDbContext db, StudioNotifier notifier, CancellationToken ct)
    {
        var video = await db.Videos.SingleOrDefaultAsync(v => v.Id == id, ct);
        if (video is null)
            return Results.NotFound();

        video.Picked = request.Picked;
        await db.SaveChangesAsync(ct);
        await notifier.SetUpdated(video.PromptSetId);
        return Results.NoContent();
    }

    private static async Task<IResult> MarkVideoViewedAsync(
        int id, StudioDbContext db, StudioNotifier notifier, TimeProvider clock, CancellationToken ct)
    {
        var video = await db.Videos.SingleOrDefaultAsync(v => v.Id == id, ct);
        if (video is null)
            return Results.NotFound();
        if (video.ViewedAt is not null)
            return Results.NoContent();

        video.ViewedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await notifier.SetUpdated(video.PromptSetId);
        return Results.NoContent();
    }

    private static async Task<IResult> GetFrameAsync(int id, string frame, StudioDbContext db, CancellationToken ct)
    {
        var paths = await db.Jobs
            .Where(j => j.Id == id)
            .Select(j => new { j.VideoStartImagePath, j.VideoEndImagePath })
            .SingleOrDefaultAsync(ct);
        return SetEndpoints.FileResult(frame switch
        {
            "start" => paths?.VideoStartImagePath,
            "end" => paths?.VideoEndImagePath,
            _ => null,
        });
    }

    private static async Task<IResult> MakeVideoAsync(
        int id, MakeVideoRequest request, StudioDbContext db, QueueService queueService, CancellationToken ct)
    {
        var frames = await FramesOfAsync(id, db, ct);
        var job = await queueService.EnqueueVideoAsync(id, new VideoJobRequest(
            frames.PathOf(request.StartFrame),
            request.EndFrame is { } endFrame ? frames.PathOf(endFrame) : null,
            request.Prompt,
            request.Seconds,
            request.Megapixels,
            request.Turbo,
            request.Count,
            request.QueueFirst), ct);
        return Results.Ok(new { job.Id });
    }
}
