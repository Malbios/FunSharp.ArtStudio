namespace ArtStudio.Server.Vision;

public sealed record VisionInstruction(string Text)
{
    public static VisionInstruction FromEmbeddedText() => new(EmbeddedText.Load("reconstruct-prompt.txt"));
}
