namespace ArtStudio.Server.Domain;

public class GeneratedVideo
{
    public int Id { get; set; }
    public int PromptSetId { get; set; }
    public int GenerationJobId { get; set; }
    public required string FilePath { get; set; }
    public long Seed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>From handing the video to ComfyUI until it was done; unknown when that spanned a restart.</summary>
    public double? RenderSeconds { get; set; }
    public bool Picked { get; set; }
    public DateTimeOffset? ViewedAt { get; set; }
}
