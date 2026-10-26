namespace ArtStudio.Server.Generation;

public static class ImageStore
{
    private static readonly Dictionary<string, string> ExtensionsByContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif",
    };

    public static string? ExtensionFor(string? contentType) =>
        contentType is not null && ExtensionsByContentType.TryGetValue(contentType.Split(';')[0].Trim(), out var extension)
            ? extension
            : null;

    // Only for serving generated videos; uploads stay limited to the image types above.
    private static readonly Dictionary<string, string> VideoContentTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp4"] = "video/mp4",
        [".webm"] = "video/webm",
    };

    public static string ContentTypeFor(string path) =>
        ExtensionsByContentType.FirstOrDefault(
            pair => pair.Value.Equals(Path.GetExtension(path), StringComparison.OrdinalIgnoreCase)).Key
        ?? VideoContentTypesByExtension.GetValueOrDefault(Path.GetExtension(path))
        ?? "application/octet-stream";

    /// <summary>Saves an image a set starts from as sources/{name}{extension}, e.g. "12" or "12-end".</summary>
    public static async Task<string> SaveSourceAsync(
        string outputDirectory, string name, Stream content, string extension, CancellationToken ct)
    {
        var directory = Directory.CreateDirectory(Path.Combine(outputDirectory, "sources"));
        var path = Path.Combine(directory.FullName, $"{name}{extension}");
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
        return path;
    }

    public static async Task<string> SavePresetImageAsync(
        string presetImagesDirectory, int blockId, Stream content, string extension, CancellationToken ct)
    {
        var directory = Directory.CreateDirectory(presetImagesDirectory);
        var path = Path.Combine(directory.FullName, $"{blockId}{extension}");
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
        return path;
    }

    public static async Task<string> SaveGeneratedAsync(
        string outputDirectory, int setId, long seed, string comfyFileName, byte[] content, CancellationToken ct)
    {
        var directory = Directory.CreateDirectory(Path.Combine(outputDirectory, "generated", setId.ToString()));
        var extension = Path.GetExtension(comfyFileName) is { Length: > 0 } ext ? ext : ".png";
        var path = Path.Combine(directory.FullName, $"{DateTime.Now:yyyyMMdd-HHmmss}-{seed}{extension}");
        await File.WriteAllBytesAsync(path, content, ct);
        return path;
    }
}
