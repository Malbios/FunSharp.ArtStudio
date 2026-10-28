namespace ArtStudio.Server.Domain;

public enum JobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled,
}

public enum JobKind
{
    Image,
    Video,
}

public class GenerationJob
{
    public int Id { get; set; }
    public int PromptSetId { get; set; }
    public PromptSet PromptSet { get; set; } = null!;
    public JobKind Kind { get; set; }
    public required string Prompt { get; set; }
    public required string Resolution { get; set; }
    /// <summary>The order <see cref="Prompt"/>'s sections are sent in; null for the normal order.</summary>
    public string? ParagraphOrder { get; set; }
    public int RequestedCount { get; set; }
    public int CompletedCount { get; set; }
    public JobStatus Status { get; set; }
    public string? Error { get; set; }
    public long QueuePosition { get; set; }
    public string? CurrentComfyPromptId { get; set; }
    public long? CurrentSeed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    public double? VideoSeconds { get; set; }
    public double? VideoMegapixels { get; set; }
    public bool VideoTurbo { get; set; }
    public string? VideoStartImagePath { get; set; }
    public string? VideoEndImagePath { get; set; }
    /// <summary>The finished video's path in ComfyUI's output folder ("video/name.mp4") until it is downloaded from Drive.</summary>
    public string? CurrentOutputFileName { get; set; }
    /// <summary>When the video ComfyUI is rendering now was handed to it.</summary>
    public DateTimeOffset? CurrentRunStartedAt { get; set; }

    public int RemainingCount => RequestedCount - CompletedCount;
}
