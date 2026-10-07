namespace ArtStudio.Server.Domain;

public class BuildingBlock
{
    public int Id { get; set; }
    public required string Label { get; set; }
    public required string Text { get; set; }
    public int SortOrder { get; set; }
}
