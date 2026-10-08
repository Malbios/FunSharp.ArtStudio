namespace ArtStudio.Server.Vision;

public sealed record VisionInstruction(string Text)
{
    private const string EmbeddedName = "ArtStudio.Server.Vision.reconstruct-prompt.txt";

    public static VisionInstruction FromEmbeddedText()
    {
        using var stream = typeof(VisionInstruction).Assembly.GetManifestResourceStream(EmbeddedName)
            ?? throw new InvalidOperationException($"Embedded instruction '{EmbeddedName}' is missing.");
        using var reader = new StreamReader(stream);
        return new VisionInstruction(reader.ReadToEnd().Trim());
    }
}
