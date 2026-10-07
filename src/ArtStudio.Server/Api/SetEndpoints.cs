using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Generation;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.Api;

public static class SetEndpoints
{
    public static void MapSetEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/resolutions", () => Resolutions.All);

        var sets = app.MapGroup("/api/sets");
        sets.MapGet("", ListSetsAsync);
        sets.MapGet("{id:int}", GetSetAsync);
        sets.MapPost("", CreateSetAsync).DisableAntiforgery();
        sets.MapPost("{id:int}/more", RequestMoreImagesAsync);
        sets.MapPost("{id:int}/select", SelectImageAsync);
        sets.MapGet("{id:int}/source", GetSourceImageAsync);
        app.MapGet("/api/images/{id:int}", GetImageAsync);
    }

    private static async Task<IReadOnlyList<SetSummaryDto>> ListSetsAsync(StudioDbContext db, CancellationToken ct)
    {
        var activeStatuses = new[] { JobStatus.Queued, JobStatus.Running };
        var sets = await db.PromptSets
            .OrderByDescending(s => s.Id)
            .Select(s => new
            {
                Set = s,
                ImageCount = s.Images.Count,
                LatestImageId = s.Images.OrderByDescending(i => i.Id).Select(i => (int?)i.Id).FirstOrDefault(),
                HasActiveJob = s.Jobs.Any(j => activeStatuses.Contains(j.Status)),
                HasFailedJob = s.Jobs.Any(j => j.Status == JobStatus.Failed),
            })
            .ToListAsync(ct);

        return sets.Select(s => new SetSummaryDto(
                s.Set.Id,
                s.Set.Prompt,
                s.Set.Resolution,
                s.Set.SourceKind,
                ApiUrls.SourceImage(s.Set),
                (s.Set.SelectedImageId ?? s.LatestImageId) is int previewId ? ApiUrls.Image(previewId) : null,
                s.ImageCount,
                s.HasActiveJob,
                s.HasFailedJob,
                s.Set.SelectedImageId,
                s.Set.CreatedAt))
            .ToList();
    }

    private static async Task<IResult> GetSetAsync(int id, StudioDbContext db, CancellationToken ct)
    {
        var set = await db.PromptSets
            .Include(s => s.Images.OrderBy(i => i.Id))
            .Include(s => s.Jobs.OrderBy(j => j.Id))
            .AsSplitQuery()
            .SingleOrDefaultAsync(s => s.Id == id, ct);
        if (set is null)
            return Results.NotFound();

        return Results.Ok(new SetDetailDto(
            set.Id,
            set.Prompt,
            set.Resolution,
            set.SourceKind,
            ApiUrls.SourceImage(set),
            set.DeviantArtUrl,
            set.DeviantArtAuthor,
            set.SelectedImageId,
            set.CreatedAt,
            set.Images.Select(ImageDto.From).ToList(),
            set.Jobs.Select(JobDto.From).ToList()));
    }

    private static async Task<IResult> CreateSetAsync(HttpRequest request, SetService setService, CancellationToken ct)
    {
        var form = await request.ReadFormAsync(ct);
        var prompt = form["prompt"].ToString();
        var resolution = form["resolution"].ToString();
        if (!int.TryParse(form["count"], out var count))
            throw new UserFacingException("Image count must be a number.");

        await using var fileStream = form.Files.GetFile("image")?.OpenReadStream();
        var source = ReadSource(form, fileStream);

        var set = await setService.CreateAsync(new NewSetRequest(prompt, resolution, count, source), ct);
        return Results.Created($"/api/sets/{set.Id}", new { set.Id });
    }

    private static SetSource ReadSource(IFormCollection form, Stream? fileStream)
    {
        if (int.TryParse(form["basedOnSetId"], out var basedOnSetId))
            return new SetSource.CopyOf(basedOnSetId);

        var sourceKind = Enum.TryParse<SourceKind>(form["sourceKind"], ignoreCase: true, out var kind) ? kind : SourceKind.None;
        switch (sourceKind)
        {
            case SourceKind.None:
                return new SetSource.None();
            case SourceKind.Upload or SourceKind.Paste:
                var file = form.Files.GetFile("image") ?? throw new UserFacingException("No image was attached.");
                var extension = ImageStore.ExtensionFor(file.ContentType)
                    ?? throw new UserFacingException($"Unsupported image type '{file.ContentType}'.");
                return new SetSource.ImageFile(sourceKind, fileStream!, extension);
            case SourceKind.DeviantArt:
                var url = form["deviantArtUrl"].ToString();
                if (string.IsNullOrWhiteSpace(url))
                    throw new UserFacingException("No DeviantArt URL was given.");
                return new SetSource.DeviantArtUrl(url);
            default:
                throw new UserFacingException($"Unsupported image source '{sourceKind}'.");
        }
    }

    private static async Task<IResult> RequestMoreImagesAsync(
        int id, MoreImagesRequest request, StudioDbContext db, QueueService queueService, CancellationToken ct)
    {
        if (!await db.PromptSets.AnyAsync(s => s.Id == id, ct))
            return Results.NotFound();
        var job = await queueService.EnqueueAsync(id, request.Count, ct);
        return Results.Ok(new { job.Id });
    }

    private static async Task<IResult> SelectImageAsync(
        int id, SelectImageRequest request, SetService setService, CancellationToken ct)
    {
        await setService.SelectImageAsync(id, request.ImageId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GetSourceImageAsync(int id, StudioDbContext db, CancellationToken ct)
    {
        var path = await db.PromptSets.Where(s => s.Id == id).Select(s => s.SourceImagePath).SingleOrDefaultAsync(ct);
        return FileResult(path);
    }

    private static async Task<IResult> GetImageAsync(int id, StudioDbContext db, CancellationToken ct)
    {
        var path = await db.Images.Where(i => i.Id == id).Select(i => i.FilePath).SingleOrDefaultAsync(ct);
        return FileResult(path);
    }

    private static IResult FileResult(string? path) =>
        path is not null && File.Exists(path)
            ? Results.File(path, ImageStore.ContentTypeFor(path), enableRangeProcessing: true)
            : Results.NotFound();
}
