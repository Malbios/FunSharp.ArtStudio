using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.Generation;

/// <summary>
/// Estimates how long a video takes from how long earlier ones took. Render time is taken to grow with frames ×
/// megapixels × sampling steps; the rate per unit is the median of recent videos, so a single slow one (e.g. one
/// that waited for ComfyUI to load the model) doesn't skew it.
/// </summary>
public sealed class VideoTimeEstimator(StudioDbContext db)
{
    private const int HistorySize = 20;
    private const int TurboSteps = 8;
    private const int FullQualitySteps = 20;

    // Until Art Studio has finished a video: the prototype's first real run, 5 s at 0.46 MP with turbo in 17:21,
    // model loading included, so the first estimates are on the high side.
    private static readonly double FallbackSecondsPerUnit = (17 * 60 + 21) / Cost(5, 0.46, turbo: true);

    public static double Cost(double seconds, double megapixels, bool turbo) =>
        VideoOptions.FrameCount(seconds) * megapixels * (turbo ? TurboSteps : FullQualitySteps);

    public async Task<double> SecondsPerUnitAsync(CancellationToken ct)
    {
        var recent = await db.Videos
            .Where(v => v.RenderSeconds != null)
            .OrderByDescending(v => v.Id)
            .Take(HistorySize)
            .Join(db.Jobs, v => v.GenerationJobId, j => j.Id,
                (v, j) => new { v.RenderSeconds, j.VideoSeconds, j.VideoMegapixels, j.VideoTurbo })
            .ToListAsync(ct);
        var rates = recent
            .Where(r => r.VideoSeconds is not null && r.VideoMegapixels is not null)
            .Select(r => r.RenderSeconds!.Value / Cost(r.VideoSeconds!.Value, r.VideoMegapixels!.Value, r.VideoTurbo))
            .Order()
            .ToList();
        return rates.Count == 0 ? FallbackSecondsPerUnit : Median(rates);
    }

    public static double? SecondsPerVideo(GenerationJob job, double secondsPerUnit) =>
        job is { Kind: JobKind.Video, VideoSeconds: { } seconds, VideoMegapixels: { } megapixels }
            ? Cost(seconds, megapixels, job.VideoTurbo) * secondsPerUnit
            : null;

    private static double Median(List<double> sorted) =>
        sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;
}
