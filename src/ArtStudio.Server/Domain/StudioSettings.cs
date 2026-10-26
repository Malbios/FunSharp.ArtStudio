namespace ArtStudio.Server.Domain;

public class StudioSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public required string ComfyServerUrl { get; set; }
    public required string OutputDirectory { get; set; }
    public bool NotifyOnJobDone { get; set; }
    public bool NotifyOnQueueEmpty { get; set; }
    public bool QueuePaused { get; set; }
    public string? DeviantArtClientId { get; set; }
    public string? ProtectedDeviantArtClientSecret { get; set; }
    public string? ProtectedDeviantArtRefreshToken { get; set; }
    public string? DeviantArtUsername { get; set; }
    public string? ProtectedVisionApiKey { get; set; }
    public string? MatureGalleryIds { get; set; }
    public string? LastGalleryId { get; set; }
    public DateTimeOffset? DeviationStatsFetchedAt { get; set; }
    public string? GoogleClientId { get; set; }
    public string? ProtectedGoogleClientSecret { get; set; }
    public string? ProtectedGoogleRefreshToken { get; set; }
    public string? GoogleAccount { get; set; }
    public DateTimeOffset? GoogleConnectedAt { get; set; }
    public string GoogleDriveOutputFolder { get; set; } = DefaultGoogleDriveOutputFolder;

    public const string DefaultGoogleDriveOutputFolder = "ComfyUI/output";
}
