using ArtStudio.Server.Comfy;
using ArtStudio.Server.Data;
using ArtStudio.Server.Generation;
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
        app.MapGet("/api/jobs/{id:int}/frames/{frame}", GetFrameAsync);
        app.MapPost("/api/sets/{id:int}/videos", MakeVideoAsync);
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
        var frameIds = new[] { request.StartImageId, request.EndImageId }.OfType<int>().ToList();
        var paths = await db.Images
            .Where(i => i.PromptSetId == id && frameIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.FilePath, ct);
        if (frameIds.Any(frameId => !paths.ContainsKey(frameId)))
            throw new UserFacingException("The start and end frames must be images of this set.");

        var job = await queueService.EnqueueVideoAsync(id, new VideoJobRequest(
            paths[request.StartImageId],
            request.EndImageId is int endId ? paths[endId] : null,
            request.Prompt,
            request.Seconds,
            request.Megapixels,
            request.Turbo,
            request.Count,
            request.QueueFirst), ct);
        return Results.Ok(new { job.Id });
    }
}
