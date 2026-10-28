using System.Globalization;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Generation;

namespace ArtStudio.Server.Api;

public sealed record ImageDto(
    int Id, int SetId, int JobId, string Url, string Seed, string Prompt, string Resolution, string? ParagraphOrder,
    DateTimeOffset CreatedAt)
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
        job.ParagraphOrder,
        image.CreatedAt);
}

public sealed record JobDto(
    int Id,
    int SetId,
    JobKind Kind,
    string Prompt,
    string Resolution,
    string? ParagraphOrder,
    string? SourceImageUrl,
    int RequestedCount,
    int CompletedCount,
    JobStatus Status,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    double? VideoSeconds,
    bool VideoTurbo,
    bool HasEndFrame,
    double? EstimatedSecondsPerVideo,
    DateTimeOffset? CurrentRunStartedAt)
{
    /// <param name="secondsPerVideoUnit">From <see cref="VideoTimeEstimator.SecondsPerUnitAsync"/>, for video estimates.</param>
    public static JobDto From(GenerationJob job, double secondsPerVideoUnit) => new(
        job.Id,
        job.PromptSetId,
        job.Kind,
        job.Prompt,
        job.Resolution,
        job.ParagraphOrder,
        // A video job shows the image it starts from.
        job.Kind == JobKind.Video ? ApiUrls.VideoFrame(job.Id, "start") : ApiUrls.SourceImage(job.PromptSet),
        job.RequestedCount,
        job.CompletedCount,
        job.Status,
        job.Error,
        job.CreatedAt,
        job.StartedAt,
        job.FinishedAt,
        job.VideoSeconds,
        job.VideoTurbo,
        job.VideoEndImagePath is not null,
        VideoTimeEstimator.SecondsPerVideo(job, secondsPerVideoUnit),
        job.CurrentRunStartedAt);
}

public sealed record VideoDto(
    int Id,
    int SetId,
    int JobId,
    string Url,
    string Seed,
    string Prompt,
    string Resolution,
    double Seconds,
    double Megapixels,
    bool Turbo,
    string StartImageUrl,
    string? EndImageUrl,
    string? StartFrameKey,
    string? EndFrameKey,
    bool Picked,
    DateTimeOffset CreatedAt)
{
    /// <param name="frames">The set's frames, to name the video's start and end frame for making it again.</param>
    public static VideoDto From(GeneratedVideo video, GenerationJob job, VideoFrames frames) => new(
        video.Id,
        video.PromptSetId,
        video.GenerationJobId,
        ApiUrls.Video(video.Id),
        video.Seed.ToString(CultureInfo.InvariantCulture),
        job.Prompt,
        job.Resolution,
        job.VideoSeconds ?? 0,
        job.VideoMegapixels ?? 0,
        job.VideoTurbo,
        ApiUrls.VideoFrame(job.Id, "start"),
        job.VideoEndImagePath is null ? null : ApiUrls.VideoFrame(job.Id, "end"),
        frames.KeyOf(job.VideoStartImagePath),
        frames.KeyOf(job.VideoEndImagePath),
        video.Picked,
        video.CreatedAt);
}

/// <summary>
/// LoadedKind is the kind ComfyUI ran last, whose model is still loaded; null after a restart. SharedServer tells
/// whether images and videos render on the same ComfyUI server, so one kind waits for the other.
/// </summary>
public sealed record QueueDto(
    bool ImagesPaused, bool VideosPaused, JobKind? LoadedKind, bool SharedServer, IReadOnlyList<JobDto> Active);

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
    PromptGenerationDto PromptGeneration,
    bool Stashed,
    string? PublishedUrl,
    DateTimeOffset? PublishedAt,
    bool HasTitleAndTags,
    int VideoCount);

public sealed record SetDetailDto(
    int Id,
    string Prompt,
    string Resolution,
    string? ParagraphOrder,
    SourceKind SourceKind,
    string? SourceImageUrl,
    string? DeviantArtUrl,
    string? DeviantArtAuthor,
    string? DeviantArtTitle,
    IReadOnlyList<int> PickedImageIds,
    DateTimeOffset CreatedAt,
    bool IsDraft,
    int DraftImageCount,
    DateTimeOffset? ReadyToPostAt,
    DateTimeOffset? ArchivedAt,
    PromptGenerationDto PromptGeneration,
    IReadOnlyList<ImageDto> Images,
    IReadOnlyList<VideoDto> Videos,
    IReadOnlyList<JobDto> Jobs,
    PostDto Post);

/// <summary>The item id is a string because DeviantArt's ids are too large for JavaScript numbers to be safe.</summary>
public sealed record PostDto(
    string? Title,
    string? Description,
    string? Tags,
    string? StashItemId,
    DateTimeOffset? StashedAt,
    string? PublishedUrl,
    DateTimeOffset? PublishedAt,
    bool Suggesting)
{
    public static PostDto From(PromptSet set, bool suggesting) => new(
        set.PostTitle, set.PostDescription, set.PostTags, set.StashItemId?.ToString(), set.StashedAt, set.PublishedUrl, set.PublishedAt,
        suggesting);
}

/// <summary>A paragraph order such as "Style,Characters,Setting,Composition"; without one, the set's latest order is used.</summary>
public sealed record MoreImagesRequest(
    int Count, string? Prompt, string? Resolution, bool QueueFirst = false, string? ParagraphOrder = null);

public sealed record QueueDraftRequest(
    string Prompt, string Resolution, int Count, bool QueueFirst = false, string? ParagraphOrder = null);

public sealed record DraftSettingsRequest(string Resolution, int ImageCount);

/// <summary>The frames are keys from GET /api/sets/{id}/video-frames, e.g. "image:12" or "source".</summary>
public sealed record MakeVideoRequest(
    string StartFrame, string? EndFrame, string Prompt, double Seconds, double Megapixels, bool Turbo, int Count,
    bool QueueFirst = false);

public sealed record VideoFrameDto(string Key, string Url, string Label);

public sealed record VideoOptionsDto(
    string PromptTemplate,
    IReadOnlyList<double> DurationPresets,
    IReadOnlyList<VideoResolutionPresetDto> ResolutionPresets,
    double DefaultSeconds,
    string DefaultResolution,
    double MaxSeconds,
    double MaxMegapixels,
    int MaxCount);

public sealed record VideoResolutionPresetDto(string Name, double Megapixels, string? Note);

public sealed record DeviantArtDraftRequest(string Url);

public sealed record PromptGenerationDto(PromptGenerationState State, PromptGenerationKind Kind, string? Error, bool Truncated, string? Text)
{
    public static PromptGenerationDto From(PromptSet set) =>
        new(set.PromptGeneration, set.ModifyInstructions is null ? PromptGenerationKind.Generate : PromptGenerationKind.Modify, set.PromptGenerationError, set.PromptGenerationTruncated, set.GeneratedPrompt);
}

public sealed record BuildingBlockDto(
    int Id,
    BuildingBlockKind Kind,
    string Label,
    string Text,
    string? ArtStyle,
    string? Resolution,
    int? ImageCount,
    string? ParagraphOrder,
    int SortOrder,
    string? ImageUrl,
    IReadOnlyList<int> PresetIds)
{
    public static BuildingBlockDto From(BuildingBlock block) => new(
        block.Id, block.Kind, block.Label, block.Text, block.ArtStyle, block.Resolution, block.ImageCount, block.ParagraphOrder,
        block.SortOrder, ApiUrls.PresetImage(block), block.BundledPresetIds());
}

public sealed record VisionSettingsDto(bool HasApiKey);

public sealed record VisionApiKeyRequest(string ApiKey);

public sealed record GeneratePromptRequest(int? ImageCount, string? Resolution, string? ParagraphOrder = null);

public sealed record ModifyPromptRequest(
    string? Prompt, string? Instructions, int? ParagraphIndex, string? Section, int? ImageCount, string? Resolution,
    string? ParagraphOrder = null);

public sealed record PickImageRequest(int ImageId);

public sealed record PickVideoRequest(bool Picked);

public sealed record ReorderPicksRequest(IReadOnlyList<int> ImageIds);

public static class ApiUrls
{
    public static string Image(int imageId) => $"/api/images/{imageId}";

    public static string Video(int videoId) => $"/api/videos/{videoId}";

    /// <summary>The start or end frame a video job renders from.</summary>
    public static string VideoFrame(int jobId, string frame) => $"/api/jobs/{jobId}/frames/{frame}";

    public static string? SourceImage(PromptSet set) =>
        set.SourceImagePath is null ? null : $"/api/sets/{set.Id}/source";

    /// <summary>Carries the file's write time so a replaced image is not served from the browser cache.</summary>
    public static string? PresetImage(BuildingBlock block) =>
        block.ImagePath is { } path && File.Exists(path)
            ? $"/api/building-blocks/{block.Id}/image?v={File.GetLastWriteTimeUtc(path).Ticks}"
            : null;
}
