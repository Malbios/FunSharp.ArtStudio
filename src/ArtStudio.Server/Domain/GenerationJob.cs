namespace ArtStudio.Server.Domain;

public enum JobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled,
}

public class GenerationJob
{
    public int Id { get; set; }
    public int PromptSetId { get; set; }
    public PromptSet PromptSet { get; set; } = null!;
    public int RequestedCount { get; set; }
    public int CompletedCount { get; set; }
    public JobStatus Status { get; set; }
    public string? Error { get; set; }
    public long QueuePosition { get; set; }
    public string? CurrentComfyPromptId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    public int RemainingCount => RequestedCount - CompletedCount;
}
