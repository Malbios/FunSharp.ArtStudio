using ArtStudio.Server.Domain;

namespace ArtStudio.Server.Api;

public sealed record ImageDto(
    int Id, int SetId, int JobId, string Url, long Seed, string Prompt, string Resolution, DateTimeOffset CreatedAt)
{
    public static ImageDto From(GeneratedImage image, GenerationJob job) => new(
        image.Id,
        image.PromptSetId,
        image.GenerationJobId,
        ApiUrls.Image(image.Id),
        image.Seed,
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

public sealed record QueueDto(bool Paused, IReadOnlyList<JobDto> Active, IReadOnlyList<JobDto> Recent);

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
    int? SelectedImageId,
    DateTimeOffset CreatedAt);

public sealed record SetDetailDto(
    int Id,
    string Prompt,
    string Resolution,
    SourceKind SourceKind,
    string? SourceImageUrl,
    string? DeviantArtUrl,
    string? DeviantArtAuthor,
    int? SelectedImageId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ImageDto> Images,
    IReadOnlyList<JobDto> Jobs);

public sealed record MoreImagesRequest(int Count, string? Prompt, string? Resolution);

public sealed record SelectImageRequest(int? ImageId);

public static class ApiUrls
{
    public static string Image(int imageId) => $"/api/images/{imageId}";

    public static string? SourceImage(PromptSet set) =>
        set.SourceImagePath is null ? null : $"/api/sets/{set.Id}/source";
}
