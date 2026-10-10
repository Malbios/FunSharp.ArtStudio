namespace ArtStudio.Server.Domain;

public enum BuildingBlockKind
{
    /// <summary>Copied to the clipboard.</summary>
    Text,

    /// <summary>Applied to the page: sets the art style box, resolution and image count when given.</summary>
    Preset,

    /// <summary>Queues one job per included preset when requeueing a set.</summary>
    Bundle,
}

public class BuildingBlock
{
    public int Id { get; set; }
    public BuildingBlockKind Kind { get; set; }
    public required string Label { get; set; }
    public required string Text { get; set; }
    public string? ArtStyle { get; set; }
    public string? Resolution { get; set; }
    public int? ImageCount { get; set; }
    public string? ImagePath { get; set; }

    /// <summary>The bundle's preset ids in order, comma-separated.</summary>
    public string? PresetIds { get; set; }

    public int SortOrder { get; set; }

    public IReadOnlyList<int> BundledPresetIds() =>
        PresetIds is null ? [] : PresetIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();

    public void SetBundledPresetIds(IEnumerable<int> ids) => PresetIds = string.Join(',', ids);
}
