using ArtStudio.Server.Domain;
using ArtStudio.Server.Vision;

namespace ArtStudio.Server.Api;

/// <summary>A prompt the vision model is working on or will work on; the note is the context or the modify instructions.</summary>
public sealed record PromptQueueItemDto(
    int SetId,
    PromptGenerationState State,
    PromptGenerationKind Kind,
    bool IsDraft,
    string? SourceImageUrl,
    string? DeviantArtAuthor,
    string? Note)
{
    public static PromptQueueItemDto From(PromptSet set)
    {
        var kind = PromptGenerationDto.From(set).Kind;
        return new(
            set.Id, set.PromptGeneration, kind, set.IsDraft, ApiUrls.SourceImage(set), set.DeviantArtAuthor,
            kind == PromptGenerationKind.Modify ? set.ModifyInstructions : set.VisionContext);
    }
}

public static class PromptQueueEndpoints
{
    public static void MapPromptQueueEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/prompt-queue", async (PromptQueueService prompts, CancellationToken ct) =>
            (await prompts.ListAsync(ct)).Select(PromptQueueItemDto.From).ToList());
        app.MapPost("/api/prompt-queue/{setId:int}/move", (int setId, MoveJobRequest request, PromptQueueService prompts, CancellationToken ct) =>
            prompts.MoveAsync(setId, request.Move, ct));
        app.MapPost("/api/prompt-queue/{setId:int}/cancel", (int setId, PromptQueueService prompts, CancellationToken ct) =>
            prompts.CancelAsync(setId, ct));
    }
}
