namespace ArtStudio.Server.Vision;

public static class EmbeddedText
{
    public static string Load(string fileName)
    {
        var name = $"ArtStudio.Server.Vision.{fileName}";
        using var stream = typeof(EmbeddedText).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded instruction '{name}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }
}
