namespace ArtStudio.Server.Domain;

public enum SourceKind
{
    None,
    Upload,
    Paste,
    DeviantArt,
}

public enum PromptGenerationState
{
    None,
    Queued,
    Running,
    Done,
    Failed,
}

public enum PromptGenerationKind
{
    Generate,
    Modify,
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
    public bool IsDraft { get; set; }
    public DateTimeOffset? ReadyToPostAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public PromptGenerationState PromptGeneration { get; set; }
    public DateTimeOffset? PromptGenerationQueuedAt { get; set; }
    public string? PromptGenerationError { get; set; }
    public bool PromptGenerationTruncated { get; set; }
    public string? GeneratedPrompt { get; set; }
    public string? ModifyInstructions { get; set; }
    public string? ModifyBasePrompt { get; set; }
    public int? ModifyParagraphIndex { get; set; }
    public string? ModifySection { get; set; }
    public int? ModifyImageCount { get; set; }
    public string? ModifyResolution { get; set; }

    public List<GenerationJob> Jobs { get; set; } = [];
    public List<GeneratedImage> Images { get; set; } = [];
    public List<PickedImage> Picks { get; set; } = [];
}
