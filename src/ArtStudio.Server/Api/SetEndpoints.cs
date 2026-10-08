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
        sets.MapPost("{id:int}/queue", async (int id, QueueDraftRequest request, SetService setService, CancellationToken ct) =>
            await setService.QueueDraftAsync(id, request.Prompt, request.Resolution, request.Count, ct));
        sets.MapPost("{id:int}/picks", async (int id, PickImageRequest request, SetService setService, CancellationToken ct) =>
            await setService.PickAsync(id, request.ImageId, ct));
        sets.MapDelete("{id:int}/picks/{imageId:int}", async (int id, int imageId, SetService setService, CancellationToken ct) =>
            await setService.UnpickAsync(id, imageId, ct));
        sets.MapPut("{id:int}/picks", async (int id, ReorderPicksRequest request, SetService setService, CancellationToken ct) =>
            await setService.ReorderPicksAsync(id, request.ImageIds, ct));
        sets.MapGet("{id:int}/source", GetSourceImageAsync);
        sets.MapPost("{id:int}/ready", async (int id, SetService setService, CancellationToken ct) =>
            await setService.MarkReadyToPostAsync(id, ct));
        sets.MapDelete("{id:int}/ready", async (int id, SetService setService, CancellationToken ct) =>
            await setService.MoveBackToSetsAsync(id, ct));
        sets.MapPost("{id:int}/generate-prompt", async (int id, GeneratePromptRequest? request, SetService setService, CancellationToken ct) =>
            await setService.QueuePromptGenerationAsync(id, request?.ImageCount, request?.Resolution, ct));
        sets.MapPost("{id:int}/modify-prompt", async (int id, ModifyPromptRequest request, SetService setService, CancellationToken ct) =>
            await setService.QueuePromptModificationAsync(
                id, request.Prompt ?? "", request.Instructions ?? "", request.ParagraphIndex, request.Section,
                request.ImageCount, request.Resolution, ct));
        sets.MapPost("{id:int}/archive", async (int id, SetService setService, CancellationToken ct) =>
            await setService.ArchiveAsync(id, ct));
        sets.MapDelete("{id:int}/archive", async (int id, SetService setService, CancellationToken ct) =>
            await setService.RestoreAsync(id, ct));
        sets.MapDelete("{id:int}", async (int id, SetService setService, CancellationToken ct) =>
            await setService.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());
        app.MapGet("/api/images/{id:int}", GetImageAsync);
        app.MapPost("/api/drafts/deviantart", async (DeviantArtDraftRequest request, SetService setService, CancellationToken ct) =>
            Results.Ok(new { (await setService.CreateDeviantArtDraftAsync(request.Url ?? "", ct)).Id }));
    }

    private static async Task<IReadOnlyList<SetSummaryDto>> ListSetsAsync(string? stage, StudioDbContext db, CancellationToken ct)
    {
        var activeStatuses = new[] { JobStatus.Queued, JobStatus.Running };
        IQueryable<PromptSet> inStage = stage switch
        {
            "draft" => db.PromptSets.Where(s => s.IsDraft).OrderBy(s => s.Id),
            null or "working" => db.PromptSets
                .Where(s => !s.IsDraft && s.ReadyToPostAt == null && s.ArchivedAt == null)
                .OrderBy(s => s.Id),
            "ready" => db.PromptSets
                .Where(s => s.ReadyToPostAt != null && s.ArchivedAt == null)
                .OrderBy(s => s.ReadyToPostAt).ThenBy(s => s.Id),
            "archived" => db.PromptSets
                .Where(s => s.ArchivedAt != null)
                .OrderByDescending(s => s.ArchivedAt).ThenByDescending(s => s.Id),
            _ => throw new UserFacingException($"Unknown stage '{stage}'. Use 'draft', 'working', 'ready' or 'archived'."),
        };
        var sets = await inStage
            .Select(s => new
            {
                Set = s,
                ImageCount = s.Images.Count,
                LatestImageId = s.Images.OrderByDescending(i => i.Id).Select(i => (int?)i.Id).FirstOrDefault(),
                FirstPickedImageId = s.Picks.OrderBy(p => p.Position).Select(p => (int?)p.GeneratedImageId).FirstOrDefault(),
                PickedCount = s.Picks.Count,
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
                (s.FirstPickedImageId ?? s.LatestImageId) is int previewId ? ApiUrls.Image(previewId) : null,
                s.ImageCount,
                s.HasActiveJob,
                s.HasFailedJob,
                s.PickedCount,
                s.Set.DeviantArtAuthor,
                s.Set.CreatedAt,
                s.Set.IsDraft,
                s.Set.ReadyToPostAt,
                s.Set.ArchivedAt,
                PromptGenerationDto.From(s.Set)))
            .ToList();
    }

    private static async Task<IResult> GetSetAsync(int id, StudioDbContext db, CancellationToken ct)
    {
        var set = await db.PromptSets
            .Include(s => s.Images.OrderBy(i => i.Id))
            .Include(s => s.Jobs.OrderBy(j => j.Id))
            .Include(s => s.Picks.OrderBy(p => p.Position))
            .AsSplitQuery()
            .SingleOrDefaultAsync(s => s.Id == id, ct);
        if (set is null)
            return Results.NotFound();
        var jobsById = set.Jobs.ToDictionary(j => j.Id);

        return Results.Ok(new SetDetailDto(
            set.Id,
            set.Prompt,
            set.Resolution,
            set.SourceKind,
            ApiUrls.SourceImage(set),
            set.DeviantArtUrl,
            set.DeviantArtAuthor,
            set.Picks.Select(p => p.GeneratedImageId).ToList(),
            set.CreatedAt,
            set.IsDraft,
            set.ReadyToPostAt,
            set.ArchivedAt,
            PromptGenerationDto.From(set),
            set.Images.Select(image => ImageDto.From(image, jobsById[image.GenerationJobId])).ToList(),
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

        var asDraft = bool.TryParse(form["draft"], out var draft) && draft;

        var set = await setService.CreateAsync(new NewSetRequest(prompt, resolution, count, source, asDraft), ct);
        return Results.Created($"/api/sets/{set.Id}", new { set.Id });
    }

    private static SetSource ReadSource(IFormCollection form, Stream? fileStream)
    {

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
                var imageIndex = int.TryParse(form["deviantArtImageIndex"], out var index) ? index : 0;
                return new SetSource.DeviantArtUrl(url, imageIndex);
            default:
                throw new UserFacingException($"Unsupported image source '{sourceKind}'.");
        }
    }

    private static async Task<IResult> RequestMoreImagesAsync(
        int id, MoreImagesRequest request, StudioDbContext db, QueueService queueService, CancellationToken ct)
    {
        if (!await db.PromptSets.AnyAsync(s => s.Id == id, ct))
            return Results.NotFound();
        var job = await queueService.EnqueueAsync(id, request.Count, request.Prompt, request.Resolution, ct);
        return Results.Ok(new { job.Id });
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
