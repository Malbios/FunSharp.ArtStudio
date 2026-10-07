namespace ArtStudio.Server.Domain;

public class BlockedArtist
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
