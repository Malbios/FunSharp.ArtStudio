namespace ArtStudio.Server.Vision;

public sealed record VisionInstruction(string Template, string ContextTemplate)
{
    private const string ContextPlaceholder = "{context}";

    public static VisionInstruction FromEmbeddedText() => new(
        EmbeddedText.Load("reconstruct-prompt.txt").ReplaceLineEndings("\n"),
        EmbeddedText.Load("reconstruct-context.txt").ReplaceLineEndings("\n"));

    public string For(string? context) =>
        string.IsNullOrWhiteSpace(context)
            ? Template.Replace(ContextPlaceholder + "\n\n", "")
            : Template.Replace(ContextPlaceholder, ContextTemplate.Replace(ContextPlaceholder, context.Trim()));
}
