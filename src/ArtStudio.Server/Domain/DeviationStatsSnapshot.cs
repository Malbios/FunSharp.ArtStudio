namespace ArtStudio.Server.Domain;

/// <summary>A published set's DeviantArt numbers on one day; a later fetch on the same day replaces them.</summary>
public class DeviationStatsSnapshot
{
    public int Id { get; set; }
    public int PromptSetId { get; set; }
    public DateOnly Day { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
    public int Views { get; set; }
    public int Favourites { get; set; }
    public int Comments { get; set; }
    public int Downloads { get; set; }
}
