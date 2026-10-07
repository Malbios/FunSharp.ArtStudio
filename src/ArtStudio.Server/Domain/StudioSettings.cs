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
}
