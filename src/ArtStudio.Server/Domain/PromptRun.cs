namespace ArtStudio.Server.Domain;

/// <summary>
/// A finished or failed vision prompt run, kept for Stats. Not tied to its set by a foreign key, so deleting a draft
/// keeps its runs counted.
/// </summary>
public class PromptRun
{
    public int Id { get; set; }
    public int PromptSetId { get; set; }
    public PromptGenerationKind Kind { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public bool Succeeded { get; set; }
}
