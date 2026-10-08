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

public sealed record BuildingBlockRequest(string Label, string Text);

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
        app.MapPost("/api/vision/modify", async (ModifyPromptRequest request, PromptModifier modifier, CancellationToken ct) =>
        {
            var answer = await modifier.ModifyAsync(request.Text, request.Instructions, request.Section, ct);
            return new ModifiedPromptDto(answer.Text, answer.Truncated);
        });

        var blocks = app.MapGroup("/api/building-blocks");
        blocks.MapGet("", async (StudioDbContext db, CancellationToken ct) =>
            await db.BuildingBlocks.OrderBy(b => b.SortOrder).ThenBy(b => b.Id).ToListAsync(ct));
        blocks.MapPost("", AddBuildingBlockAsync);
        blocks.MapPut("{id:int}", UpdateBuildingBlockAsync);
        blocks.MapDelete("{id:int}", async (int id, StudioDbContext db, CancellationToken ct) =>
        {
            await db.BuildingBlocks.Where(b => b.Id == id).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        });
        blocks.MapPost("reorder", ReorderBuildingBlocksAsync);
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

    private static async Task<BuildingBlock> AddBuildingBlockAsync(
        BuildingBlockRequest request, StudioDbContext db, CancellationToken ct)
    {
        var lastSortOrder = await db.BuildingBlocks.MaxAsync(b => (int?)b.SortOrder, ct) ?? 0;
        var block = new BuildingBlock { Label = "", Text = "", SortOrder = lastSortOrder + 1 };
        Apply(block, request);
        db.BuildingBlocks.Add(block);
        await db.SaveChangesAsync(ct);
        return block;
    }

    private static async Task<IResult> UpdateBuildingBlockAsync(
        int id, BuildingBlockRequest request, StudioDbContext db, CancellationToken ct)
    {
        var block = await db.BuildingBlocks.FindAsync([id], ct);
        if (block is null)
            return Results.NotFound();
        Apply(block, request);
        await db.SaveChangesAsync(ct);
        return Results.Ok(block);
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

    private static void Apply(BuildingBlock block, BuildingBlockRequest request)
    {
        if (string.IsNullOrEmpty(request.Text))
            throw new UserFacingException("A building block needs some text.");
        block.Text = request.Text;
        block.Label = string.IsNullOrWhiteSpace(request.Label) ? block.Text.Trim() : request.Label.Trim();
    }
}
