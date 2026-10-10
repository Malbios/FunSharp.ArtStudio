using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Generation;
using ArtStudio.Server.Vision;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.Api;

public sealed record SettingsDto(string ComfyServerUrl, string OutputDirectory, bool NotifyOnJobDone, bool NotifyOnQueueEmpty)
{
    public static SettingsDto From(StudioSettings settings) => new(
        settings.ComfyServerUrl, settings.OutputDirectory, settings.NotifyOnJobDone, settings.NotifyOnQueueEmpty);
}

public sealed record BuildingBlockRequest(
    string Label,
    string? Text,
    BuildingBlockKind Kind = BuildingBlockKind.Text,
    string? ArtStyle = null,
    string? Resolution = null,
    int? ImageCount = null,
    IReadOnlyList<int>? PresetIds = null);

public sealed record ReorderRequest(IReadOnlyList<int> Ids);

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/settings", async (StudioDbContext db, AppPaths paths, CancellationToken ct) =>
            SettingsDto.From(await SettingsStore.LoadAsync(db, paths, ct)));
        app.MapPut("/api/settings", UpdateSettingsAsync);

        app.MapGet("/api/vision", async (VisionApiKey apiKey, CancellationToken ct) =>
            new VisionSettingsDto(await apiKey.HasKeyAsync(ct)));
        app.MapPut("/api/vision", async (VisionApiKeyRequest request, VisionApiKey apiKey, CancellationToken ct) =>
        {
            await apiKey.SaveAsync(request.ApiKey ?? "", ct);
            return new VisionSettingsDto(true);
        });
        app.MapDelete("/api/vision", async (VisionApiKey apiKey, CancellationToken ct) =>
        {
            await apiKey.ClearAsync(ct);
            return new VisionSettingsDto(false);
        });

        var blocks = app.MapGroup("/api/building-blocks");
        blocks.MapGet("", async (StudioDbContext db, CancellationToken ct) =>
            (await db.BuildingBlocks.OrderBy(b => b.SortOrder).ThenBy(b => b.Id).ToListAsync(ct)).Select(BuildingBlockDto.From));
        blocks.MapPost("", AddBuildingBlockAsync);
        blocks.MapPut("{id:int}", UpdateBuildingBlockAsync);
        blocks.MapDelete("{id:int}", DeleteBuildingBlockAsync);
        blocks.MapPost("reorder", ReorderBuildingBlocksAsync);
        blocks.MapGet("{id:int}/image", async (int id, StudioDbContext db, CancellationToken ct) =>
            SetEndpoints.FileResult(await db.BuildingBlocks.Where(b => b.Id == id).Select(b => b.ImagePath).SingleOrDefaultAsync(ct)));
        blocks.MapPut("{id:int}/image", SetPresetImageAsync).DisableAntiforgery();
        blocks.MapDelete("{id:int}/image", RemovePresetImageAsync);
    }

    private static async Task<SettingsDto> UpdateSettingsAsync(
        SettingsDto request, StudioDbContext db, AppPaths paths, CancellationToken ct)
    {
        if (!Uri.TryCreate(request.ComfyServerUrl.Trim(), UriKind.Absolute, out var comfyUrl) ||
            comfyUrl.Scheme is not ("http" or "https"))
            throw new UserFacingException("The ComfyUI server URL must be an absolute http(s) URL.");

        var outputDirectory = request.OutputDirectory.Trim();
        if (!Path.IsPathFullyQualified(outputDirectory))
            throw new UserFacingException("The output directory must be a full path, for example C:\\Images.");
        try
        {
            Directory.CreateDirectory(outputDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new UserFacingException($"The output directory cannot be used: {ex.Message}");
        }

        var settings = await SettingsStore.LoadAsync(db, paths, ct);
        settings.ComfyServerUrl = comfyUrl.ToString().TrimEnd('/');
        settings.OutputDirectory = outputDirectory;
        settings.NotifyOnJobDone = request.NotifyOnJobDone;
        settings.NotifyOnQueueEmpty = request.NotifyOnQueueEmpty;
        await db.SaveChangesAsync(ct);
        return SettingsDto.From(settings);
    }

    private static async Task<BuildingBlockDto> AddBuildingBlockAsync(
        BuildingBlockRequest request, StudioDbContext db, CancellationToken ct)
    {
        var lastSortOrder = await db.BuildingBlocks.MaxAsync(b => (int?)b.SortOrder, ct) ?? 0;
        var block = new BuildingBlock { Label = "", Text = "", SortOrder = lastSortOrder + 1 };
        await ApplyAsync(block, request, db, ct);
        db.BuildingBlocks.Add(block);
        await db.SaveChangesAsync(ct);
        return BuildingBlockDto.From(block);
    }

    private static async Task<IResult> UpdateBuildingBlockAsync(
        int id, BuildingBlockRequest request, StudioDbContext db, CancellationToken ct)
    {
        var block = await db.BuildingBlocks.FindAsync([id], ct);
        if (block is null)
            return Results.NotFound();
        await ApplyAsync(block, request, db, ct);
        if (block.Kind != BuildingBlockKind.Preset)
            DeleteImageFile(block);
        await db.SaveChangesAsync(ct);
        return Results.Ok(BuildingBlockDto.From(block));
    }

    private static async Task<IResult> DeleteBuildingBlockAsync(int id, StudioDbContext db, CancellationToken ct)
    {
        if (await db.BuildingBlocks.FindAsync([id], ct) is { } block)
        {
            DeleteImageFile(block);
            db.BuildingBlocks.Remove(block);
            await RemoveFromBundlesAsync(block, db, ct);
            await db.SaveChangesAsync(ct);
        }
        return Results.NoContent();
    }

    private static async Task<IResult> SetPresetImageAsync(
        int id, HttpRequest request, StudioDbContext db, AppPaths paths, CancellationToken ct)
    {
        var block = await db.BuildingBlocks.FindAsync([id], ct);
        if (block is null)
            return Results.NotFound();
        if (block.Kind != BuildingBlockKind.Preset)
            throw new UserFacingException("Only presets can have an image.");

        var file = (await request.ReadFormAsync(ct)).Files.GetFile("image")
            ?? throw new UserFacingException("Choose an image.");
        var extension = ImageStore.ExtensionFor(file.ContentType)
            ?? throw new UserFacingException($"Unsupported image type '{file.ContentType}'. Use PNG, JPEG, WebP or GIF.");

        await using var content = file.OpenReadStream();
        var path = await ImageStore.SavePresetImageAsync(paths.PresetImagesDirectory, id, content, extension, ct);
        if (block.ImagePath != path)
            DeleteImageFile(block);
        block.ImagePath = path;
        await db.SaveChangesAsync(ct);
        return Results.Ok(BuildingBlockDto.From(block));
    }

    private static async Task<IResult> RemovePresetImageAsync(int id, StudioDbContext db, CancellationToken ct)
    {
        var block = await db.BuildingBlocks.FindAsync([id], ct);
        if (block is null)
            return Results.NotFound();
        DeleteImageFile(block);
        await db.SaveChangesAsync(ct);
        return Results.Ok(BuildingBlockDto.From(block));
    }

    private static async Task RemoveFromBundlesAsync(BuildingBlock preset, StudioDbContext db, CancellationToken ct)
    {
        if (preset.Kind != BuildingBlockKind.Preset)
            return;
        var bundles = await db.BuildingBlocks.Where(b => b.Kind == BuildingBlockKind.Bundle).ToListAsync(ct);
        foreach (var bundle in bundles.Where(b => b.BundledPresetIds().Contains(preset.Id)))
            bundle.SetBundledPresetIds(bundle.BundledPresetIds().Where(id => id != preset.Id));
    }

    private static void DeleteImageFile(BuildingBlock block)
    {
        if (block.ImagePath is { } path && File.Exists(path))
            File.Delete(path);
        block.ImagePath = null;
    }

    private static async Task ReorderBuildingBlocksAsync(ReorderRequest request, StudioDbContext db, CancellationToken ct)
    {
        var blocks = await db.BuildingBlocks.ToDictionaryAsync(b => b.Id, ct);
        for (var index = 0; index < request.Ids.Count; index++)
        {
            if (blocks.TryGetValue(request.Ids[index], out var block))
                block.SortOrder = index + 1;
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task ApplyAsync(
        BuildingBlock block, BuildingBlockRequest request, StudioDbContext db, CancellationToken ct)
    {
        switch (request.Kind)
        {
            case BuildingBlockKind.Preset:
                ApplyPreset(block, request);
                break;
            case BuildingBlockKind.Bundle:
                await ApplyBundleAsync(block, request, db, ct);
                break;
            default:
                ApplyText(block, request);
                break;
        }
        if (block.Kind != BuildingBlockKind.Bundle)
            block.PresetIds = null;
    }

    private static async Task ApplyBundleAsync(
        BuildingBlock block, BuildingBlockRequest request, StudioDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
            throw new UserFacingException("A preset bundle needs a label.");
        var ids = request.PresetIds ?? [];
        if (ids.Count == 0)
            throw new UserFacingException("A preset bundle needs at least one preset.");
        if (ids.Distinct().Count() != ids.Count)
            throw new UserFacingException("A preset bundle can contain each preset only once.");
        var presetCount = await db.BuildingBlocks.CountAsync(b => ids.Contains(b.Id) && b.Kind == BuildingBlockKind.Preset, ct);
        if (presetCount != ids.Count)
            throw new UserFacingException("A preset bundle can only contain existing presets.");

        block.Kind = BuildingBlockKind.Bundle;
        block.Label = request.Label.Trim();
        block.Text = "";
        block.ArtStyle = null;
        block.Resolution = null;
        block.ImageCount = null;
        block.SetBundledPresetIds(ids);
    }

    private static void ApplyText(BuildingBlock block, BuildingBlockRequest request)
    {
        if (string.IsNullOrEmpty(request.Text))
            throw new UserFacingException("A building block needs some text.");
        block.Kind = BuildingBlockKind.Text;
        block.Text = request.Text;
        block.Label = string.IsNullOrWhiteSpace(request.Label) ? block.Text.Trim() : request.Label.Trim();
        block.ArtStyle = null;
        block.Resolution = null;
        block.ImageCount = null;
    }

    private static void ApplyPreset(BuildingBlock block, BuildingBlockRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
            throw new UserFacingException("A preset needs a label.");
        var artStyle = string.IsNullOrWhiteSpace(request.ArtStyle) ? null : request.ArtStyle.Trim();
        var resolution = string.IsNullOrWhiteSpace(request.Resolution)
            ? null
            : Resolutions.Find(request.Resolution)?.Name
                ?? throw new UserFacingException($"Unknown resolution '{request.Resolution}'.");
        if (request.ImageCount is < 1 or > QueueService.MaxImagesPerJob)
            throw new UserFacingException($"Image count must be between 1 and {QueueService.MaxImagesPerJob}.");
        if (artStyle is null && resolution is null && request.ImageCount is null)
            throw new UserFacingException("A preset needs an art style, a resolution or an image count.");

        block.Kind = BuildingBlockKind.Preset;
        block.Label = request.Label.Trim();
        block.Text = "";
        block.ArtStyle = artStyle;
        block.Resolution = resolution;
        block.ImageCount = request.ImageCount;
    }
}
