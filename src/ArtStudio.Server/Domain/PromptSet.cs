namespace ArtStudio.Server.Domain;

public enum SourceKind
{
    None,
    Upload,
    Paste,
    DeviantArt,
}

public class PromptSet
{
    public int Id { get; set; }
    public required string Prompt { get; set; }
    public required string Resolution { get; set; }
    public SourceKind SourceKind { get; set; }
    public string? SourceImagePath { get; set; }
    public string? DeviantArtUrl { get; set; }
    public string? DeviationId { get; set; }
    public string? DeviantArtAuthor { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public List<GenerationJob> Jobs { get; set; } = [];
    public List<GeneratedImage> Images { get; set; } = [];
    public List<PickedImage> Picks { get; set; } = [];
}
