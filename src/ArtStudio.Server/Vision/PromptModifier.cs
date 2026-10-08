using System.Text.RegularExpressions;
using ArtStudio.Server.Generation;

namespace ArtStudio.Server.Vision;

/// <summary>Rewrites a whole prompt, or one of its paragraphs, by the user's instructions. Text only, no image.</summary>
public sealed partial class PromptModifier(VisionClient visionClient, VisionApiKey apiKey)
{
    private static readonly string PromptTemplate = EmbeddedText.Load("modify-prompt.txt");
    private static readonly string ParagraphTemplate = EmbeddedText.Load("modify-paragraph.txt");

    public async Task<VisionAnswer> ModifyAsync(string? text, string? instructions, string? section, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new UserFacingException("There is no prompt text to modify.");
        if (string.IsNullOrWhiteSpace(instructions))
            throw new UserFacingException("Describe how the prompt should change.");
        var key = await apiKey.GetAsync(ct)
            ?? throw new UserFacingException("Set the vision API key in Settings.");

        try
        {
            var answer = await visionClient.CompleteAsync(key, BuildInstruction(text.Trim(), instructions.Trim(), section?.Trim()), ct);
            return answer with { Text = PromptCleaner.Clean(answer.Text) };
        }
        catch (VisionException ex)
        {
            throw new UserFacingException(ex.Message);
        }
    }

    private static string BuildInstruction(string text, string instructions, string? section) =>
        string.IsNullOrEmpty(section)
            ? Fill(PromptTemplate, new() { ["prompt"] = text, ["instructions"] = instructions })
            : Fill(ParagraphTemplate, new() { ["paragraph"] = text, ["instructions"] = instructions, ["section"] = section });

    private static string Fill(string template, Dictionary<string, string> values) =>
        Placeholder().Replace(template, match => values.GetValueOrDefault(match.Groups[1].Value, match.Value));

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();
}
