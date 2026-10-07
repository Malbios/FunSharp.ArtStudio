namespace ArtStudio.Server.Generation;

public sealed record ResolutionPreset(string Name, int Width, int Height, string ComfyLabel)
{
    public double AspectRatio => (double)Width / Height;
}

public static class Resolutions
{
    public static readonly IReadOnlyList<ResolutionPreset> All =
    [
        new("Native", 1024, 1024, "1024×1024 (1:1) - Native"),
        new("Landscape", 1152, 896, "1152×896 (9:7) - Landscape"),
        new("Portrait", 896, 1152, "896×1152 (7:9) - Portrait"),
        new("Wide", 1344, 768, "1344×768 (7:4) - Wide"),
        new("Tall", 768, 1344, "768×1344 (4:7) - Tall"),
        new("Cinema", 1216, 832, "1216×832 (19:13) - Cinema"),
        new("TallCinema", 832, 1216, "832×1216 (13:19) - Tall Cinema"),
        new("Ultrawide", 1280, 768, "1280×768 (5:3) - Ultrawide"),
        new("UltraPortrait", 768, 1280, "768×1280 (3:5) - Ultra Portrait"),
        new("Banner", 1536, 640, "1536×640 (12:5) - Banner"),
        new("Skyscraper", 640, 1536, "640×1536 (5:12) - Skyscraper"),
        new("ExtremeWide", 1600, 640, "1600×640 (5:2) - Extreme Wide"),
        new("ExtremeTall", 640, 1600, "640×1600 (2:5) - Extreme Tall"),
    ];

    public static ResolutionPreset? Find(string name) =>
        All.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    public static ResolutionPreset ClosestTo(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var logAspect = Math.Log((double)width / height);
        return All.MinBy(p => Math.Abs(logAspect - Math.Log(p.AspectRatio)))!;
    }
}
