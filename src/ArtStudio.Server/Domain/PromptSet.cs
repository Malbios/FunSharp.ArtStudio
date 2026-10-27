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
    public string? DeviantArtTitle { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsDraft { get; set; }
    public int DraftImageCount { get; set; } = Generation.SetService.DefaultImageCount;
    public DateTimeOffset? ReadyToPostAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public PromptGenerationState PromptGeneration { get; set; }
    public DateTimeOffset? PromptGenerationQueuedAt { get; set; }
    /// <summary>Clicked for rather than queued automatically for a new draft; such prompts go first.</summary>
    public bool PromptGenerationRequested { get; set; }
    public string? PromptGenerationError { get; set; }
    public bool PromptGenerationTruncated { get; set; }
    public string? GeneratedPrompt { get; set; }
    public string? ModifyInstructions { get; set; }
    public string? ModifyBasePrompt { get; set; }
    public int? ModifyParagraphIndex { get; set; }
    public string? ModifySection { get; set; }
    public int? ImagesAfterPromptCount { get; set; }
    public string? ImagesAfterPromptResolution { get; set; }
    public string? PostTitle { get; set; }
    public string? PostDescription { get; set; }
    public string? PostTags { get; set; }
    public long? StashItemId { get; set; }
    public DateTimeOffset? StashedAt { get; set; }
    public string? PublishedUrl { get; set; }
    public string? PublishedDeviationId { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    public List<GenerationJob> Jobs { get; set; } = [];
    public List<GeneratedImage> Images { get; set; } = [];
    public List<PickedImage> Picks { get; set; } = [];
}
