namespace ArtStudio.Server.Domain;

public class PickedImage
{
    public int Id { get; set; }
    public int PromptSetId { get; set; }
    public int GeneratedImageId { get; set; }
    public int Position { get; set; }
}
