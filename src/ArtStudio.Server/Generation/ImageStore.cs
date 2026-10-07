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

    public static string ContentTypeFor(string path) =>
        ExtensionsByContentType.FirstOrDefault(
            pair => pair.Value.Equals(Path.GetExtension(path), StringComparison.OrdinalIgnoreCase)).Key
        ?? "application/octet-stream";

    public static async Task<string> SaveSourceAsync(
        string outputDirectory, int setId, Stream content, string extension, CancellationToken ct)
    {
        var directory = Directory.CreateDirectory(Path.Combine(outputDirectory, "sources"));
        var path = Path.Combine(directory.FullName, $"{setId}{extension}");
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
