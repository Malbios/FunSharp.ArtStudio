namespace ArtStudio.Server.Domain;

public class GeneratedVideo
{
    public int Id { get; set; }
    public int PromptSetId { get; set; }
    public int GenerationJobId { get; set; }
    public required string FilePath { get; set; }
    public long Seed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
