using ArtStudio.Server.Domain;

namespace ArtStudio.Server.Stats;

public sealed record StatsDto(
    DeviantArtStatsDto DeviantArt,
    IReadOnlyList<PresetStatsDto> Presets,
    PipelineStatsDto Pipeline,
    InspirationStatsDto Inspiration);

public sealed record DeviantArtStatsDto(
    bool Connected,
    DateTimeOffset? FetchedAt,
    bool Stale,
    DateOnly? HistorySince,
    DeviantArtTotalsDto Totals,
    IReadOnlyList<DeviationStatsDto> Deviations,
    IReadOnlyList<DailyGainDto> Daily);

public sealed record DeviantArtTotalsDto(
    int Published,
    int Tracked,
    int Views,
    int Favourites,
    int Comments,
    int Downloads,
    int ViewsLast7Days,
    int FavouritesLast7Days);

/// <summary>The numbers are null until the deviation's stats were first fetched.</summary>
public sealed record DeviationStatsDto(
    int SetId,
    string? Title,
    string Url,
    string? PreviewImageUrl,
    DateTimeOffset PublishedAt,
    double DaysOnline,
    string? Preset,
    int? Views,
    int? Favourites,
    int? Comments,
    int? Downloads,
    double? FavouritesPer100Views,
    double? ViewsPerDay,
    int? ViewsLast7Days,
    int? FavouritesLast7Days);

public sealed record DailyGainDto(DateOnly Day, int Views, int Favourites);

/// <summary>A null preset id is the row of images whose prompt matches no preset's art style.</summary>
public sealed record PresetStatsDto(
    int? PresetId,
    string Name,
    int Images,
    int Picks,
    double? PickRate,
    int Published,
    double? AverageViews,
    double? AverageFavourites);

public sealed record StageCountsDto(int Drafts, int Working, int Post, int Stashed, int Published, int Archived);

public sealed record DailyCountDto(DateOnly Day, int Count);

public sealed record PipelineStatsDto(
    StageCountsDto Stages,
    int Images,
    int ImagesLast7Days,
    int ImagesLast30Days,
    IReadOnlyList<DailyCountDto> ImagesPerDay,
    double? PickRate,
    double? SecondsPerImage,
    int JobsCompletedLast30Days,
    int JobsFailedLast30Days,
    double? MedianDaysToReady,
    double? MedianDaysReadyToPublished);

public sealed record SourceCountDto(SourceKind Source, int Sets);

public sealed record InspiringArtistDto(string Username, int Sets, int Published, double? AverageFavourites);

public sealed record InspirationStatsDto(IReadOnlyList<SourceCountDto> Sources, IReadOnlyList<InspiringArtistDto> TopArtists);
