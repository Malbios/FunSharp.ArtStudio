using System.Globalization;
using ArtStudio.Server.Domain;

namespace ArtStudio.Server.Api;

public sealed record ImageDto(
    int Id, int SetId, int JobId, string Url, string Seed, string Prompt, string Resolution, DateTimeOffset CreatedAt)
{
    public static ImageDto From(GeneratedImage image, GenerationJob job) => new(
        image.Id,
        image.PromptSetId,
        image.GenerationJobId,
        ApiUrls.Image(image.Id),
        // Seeds exceed JavaScript's exact integer range, so they travel as text.
        image.Seed.ToString(CultureInfo.InvariantCulture),
        job.Prompt,
        job.Resolution,
        image.CreatedAt);
}

public sealed record JobDto(
    int Id,
    int SetId,
    string Prompt,
    string Resolution,
    string? SourceImageUrl,
    int RequestedCount,
    int CompletedCount,
    JobStatus Status,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt)
{
    public static JobDto From(GenerationJob job) => new(
        job.Id,
        job.PromptSetId,
        job.Prompt,
        job.Resolution,
        ApiUrls.SourceImage(job.PromptSet),
        job.RequestedCount,
        job.CompletedCount,
        job.Status,
        job.Error,
        job.CreatedAt,
        job.StartedAt,
        job.FinishedAt);
}

public sealed record QueueDto(bool Paused, IReadOnlyList<JobDto> Active);

public sealed record SetSummaryDto(
    int Id,
    string Prompt,
    string Resolution,
    SourceKind SourceKind,
    string? SourceImageUrl,
    string? PreviewImageUrl,
    int ImageCount,
    bool HasActiveJob,
    bool HasFailedJob,
    int PickedCount,
    string? DeviantArtAuthor,
    DateTimeOffset CreatedAt,
    bool IsDraft,
    DateTimeOffset? ReadyToPostAt,
    DateTimeOffset? ArchivedAt,
    PromptGenerationDto PromptGeneration);

public sealed record SetDetailDto(
    int Id,
    string Prompt,
    string Resolution,
    SourceKind SourceKind,
    string? SourceImageUrl,
    string? DeviantArtUrl,
    string? DeviantArtAuthor,
    IReadOnlyList<int> PickedImageIds,
    DateTimeOffset CreatedAt,
    bool IsDraft,
    DateTimeOffset? ReadyToPostAt,
    DateTimeOffset? ArchivedAt,
    PromptGenerationDto PromptGeneration,
    IReadOnlyList<ImageDto> Images,
    IReadOnlyList<JobDto> Jobs);

public sealed record MoreImagesRequest(int Count, string? Prompt, string? Resolution);

public sealed record QueueDraftRequest(string Prompt, string Resolution, int Count);

public sealed record DeviantArtDraftRequest(string Url);

public sealed record PromptGenerationDto(PromptGenerationState State, string? Error, bool Truncated, string? Text)
{
    public static PromptGenerationDto From(PromptSet set) =>
        new(set.PromptGeneration, set.PromptGenerationError, set.PromptGenerationTruncated, set.GeneratedPrompt);
}

public sealed record VisionSettingsDto(bool HasApiKey);

public sealed record VisionApiKeyRequest(string ApiKey);

public sealed record PickImageRequest(int ImageId);

public sealed record ReorderPicksRequest(IReadOnlyList<int> ImageIds);

public static class ApiUrls
{
    public static string Image(int imageId) => $"/api/images/{imageId}";

    public static string? SourceImage(PromptSet set) =>
        set.SourceImagePath is null ? null : $"/api/sets/{set.Id}/source";
}
