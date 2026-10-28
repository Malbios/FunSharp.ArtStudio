using System.Globalization;

namespace ArtStudio.Server.Generation;

public sealed record VideoResolutionPreset(string Name, double Megapixels, string? Note = null);

/// <summary>The MiniMax H3 workflow's duration and resolution choices and their limits.</summary>
public static class VideoOptions
{
    public const double DefaultSeconds = 5;
    public const string DefaultResolution = "864 x 480";
    public const double MaxSeconds = 30;
    // The workflow's notes: H3's canvas is capped at 768x1344, which is its 0.98 megapixel preset.
    public const double MaxMegapixels = 0.98;
    public const int MaxVideosPerJob = 20;

    public static readonly IReadOnlyList<double> DurationPresets = [3, 5, 6, 8, 10, 15, 20, 30];

    /// <summary>
    /// Named after the size a 16:9 start image gets; other shapes keep the pixel count. The notes are the workflow's.
    /// </summary>
    public static readonly IReadOnlyList<VideoResolutionPreset> ResolutionPresets =
    [
        new("608 x 352", 0.2),
        new("736 x 416", 0.3),
        new("864 x 480", 0.4, "official 480p"),
        new("896 x 512", 0.46),
        new("960 x 544", 0.5),
        new("1056 x 608", 0.6),
        new("1152 x 640", 0.7),
        new("1216 x 672", 0.8),
        new("1280 x 736", 0.9),
        new("1344 x 768", 0.98, "official 768p, the largest"),
    ];

    private const int FramesPerSecond = 24;
    private const int FrameBlock = 17;
    private const int FrameOffset = 5;

    /// <summary>The workflow's own snapping: frames at 24 fps, rounded up to the model's 17k+5 frame grid.</summary>
    public static int FrameCount(double seconds)
    {
        var frames = Math.Max(FrameOffset, (int)Math.Round(seconds * FramesPerSecond));
        return frames + ((FrameOffset - frames % FrameBlock) % FrameBlock + FrameBlock) % FrameBlock;
    }

    public static string ResolutionLabel(double megapixels) =>
        ResolutionPresets.FirstOrDefault(p => Math.Abs(p.Megapixels - megapixels) < 0.0001)?.Name
        ?? $"{megapixels.ToString("0.###", CultureInfo.InvariantCulture)} MP";

    public static void Validate(double seconds, double megapixels, int count)
    {
        if (seconds is <= 0 or > MaxSeconds)
            throw new UserFacingException($"The video length must be between 0 and {MaxSeconds} seconds.");
        if (megapixels is <= 0 or > MaxMegapixels)
            throw new UserFacingException($"The video size must be between 0 and {MaxMegapixels} megapixels.");
        if (count is < 1 or > MaxVideosPerJob)
            throw new UserFacingException($"The number of videos must be between 1 and {MaxVideosPerJob}.");
    }
}
