using System.Text.RegularExpressions;

namespace ArtStudio.Server.Generation;

/// <summary>The same rules the web app applies to pasted prompts (src/prompt/cleanPrompt.ts); keep both in sync.</summary>
public static partial class PromptCleaner
{
    [GeneratedRegex(@"\r\n?")]
    private static partial Regex LineBreak();

    [GeneratedRegex(@"[ \t]*(?<![\p{L}\p{N}_-])adult(?![\p{L}\p{N}_-])[ \t]*", RegexOptions.IgnoreCase)]
    private static partial Regex RemovedWord();

    [GeneratedRegex(@",(?:[ \t]*,)+")]
    private static partial Regex RepeatedCommas();

    [GeneratedRegex(@"^[,.;:!?]")]
    private static partial Regex StartsWithPunctuation();

    public static string Clean(string text)
    {
        var cleaned = LineBreak().Replace(text, "\n").Replace('’', '\'');
        cleaned = RemovedWord().Replace(cleaned, match => ReplaceRemovedWord(match, cleaned));
        return RepeatedCommas().Replace(cleaned, ",");
    }

    private static string ReplaceRemovedWord(Match match, string text)
    {
        var before = text[..match.Index];
        var after = text[(match.Index + match.Length)..];
        var atLineStart = before.Length == 0 || before.EndsWith('\n');
        var atLineEnd = after.Length == 0 || after.StartsWith('\n');
        return atLineStart || atLineEnd || StartsWithPunctuation().IsMatch(after) ? "" : " ";
    }
}
