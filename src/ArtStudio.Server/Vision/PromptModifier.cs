using System.Text.RegularExpressions;
using ArtStudio.Server.Generation;

namespace ArtStudio.Server.Vision;

/// <summary>Rewrites a whole prompt, or one of its paragraphs, by the user's instructions. Text only, no image.</summary>
public static partial class PromptModifier
{
    private static readonly string PromptTemplate = EmbeddedText.Load("modify-prompt.txt");
    private static readonly string ParagraphTemplate = EmbeddedText.Load("modify-paragraph.txt");

    public static IReadOnlyList<string> Paragraphs(string prompt) =>
        ParagraphBreak().Split(prompt.ReplaceLineEndings("\n"))
            .Select(paragraph => paragraph.Trim())
            .Where(paragraph => paragraph.Length > 0)
            .ToList();

    public static string BuildInstruction(string basePrompt, int? paragraphIndex, string? section, string instructions) =>
        paragraphIndex is { } index
            ? Fill(ParagraphTemplate, new()
            {
                ["paragraph"] = Paragraphs(basePrompt)[index],
                ["instructions"] = instructions,
                ["section"] = section ?? "",
            })
            : Fill(PromptTemplate, new() { ["prompt"] = basePrompt, ["instructions"] = instructions });

    /// <summary>The new prompt: the cleaned answer, or the base prompt with only the modified paragraph replaced.</summary>
    public static string Apply(string basePrompt, int? paragraphIndex, string answer)
    {
        var cleaned = PromptCleaner.Clean(answer);
        if (paragraphIndex is not { } index)
            return cleaned;

        var paragraphs = Paragraphs(basePrompt).ToList();
        paragraphs[index] = string.Join(' ', Paragraphs(cleaned));
        return string.Join("\n\n", paragraphs);
    }

    private static string Fill(string template, Dictionary<string, string> values) =>
        Placeholder().Replace(template, match => values.GetValueOrDefault(match.Groups[1].Value, match.Value));

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex ParagraphBreak();
}
