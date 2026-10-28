using ArtStudio.Server.Vision;

namespace ArtStudio.Server.Generation;

/// <summary>
/// The order a run sends the prompt's sections in, e.g. "Style,Characters,Setting,Composition". Prompts are stored in
/// the normal order (characters, setting, composition, style), so sections are known by their position from the end.
/// </summary>
public static class ParagraphOrder
{
    private const string Characters = "Characters";
    private static readonly string[] FixedSections = ["Setting", "Composition", "Style"];
    private static readonly string[] NormalOrder = [Characters, .. FixedSections];

    public static readonly string Normal = string.Join(',', NormalOrder);

    /// <summary>The order as stored: null for the normal order.</summary>
    public static string? Normalize(string? order)
    {
        if (string.IsNullOrWhiteSpace(order))
            return null;
        var sections = order.Split(',', StringSplitOptions.TrimEntries);
        if (sections.Length != NormalOrder.Length || sections.Except(NormalOrder).Any() || sections.Distinct().Count() != sections.Length)
            throw new UserFacingException(
                $"The paragraph order '{order}' must list each of {string.Join(", ", NormalOrder)} once.");
        return sections.SequenceEqual(NormalOrder) ? null : string.Join(',', sections);
    }

    /// <summary>A checked order that keeps the normal order explicit, as presets need; null when none is given.</summary>
    public static string? Validated(string? order) => string.IsNullOrWhiteSpace(order) ? null : Normalize(order) ?? Normal;

    /// <summary>The prompt with its sections in the given order; prompts without all fixed sections stay as they are.</summary>
    public static string Apply(string prompt, string? order)
    {
        if (Normalize(order) is not { } normalized)
            return prompt;
        var paragraphs = PromptModifier.Paragraphs(prompt);
        if (paragraphs.Count < FixedSections.Length)
            return prompt;

        var characterCount = paragraphs.Count - FixedSections.Length;
        var bySection = new Dictionary<string, IEnumerable<string>> { [Characters] = paragraphs.Take(characterCount) };
        for (var index = 0; index < FixedSections.Length; index++)
            bySection[FixedSections[index]] = [paragraphs[characterCount + index]];
        return string.Join("\n\n", normalized.Split(',').SelectMany(section => bySection[section]));
    }
}
