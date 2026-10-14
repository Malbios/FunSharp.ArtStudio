using System.Text.RegularExpressions;
using ArtStudio.Server.Data;
using ArtStudio.Server.DeviantArt;
using ArtStudio.Server.Generation;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.Vision;

public sealed partial class TagSuggester(StudioDbContext db, VisionClient vision, VisionApiKey apiKey, DeviantArtPosting posting)
{
    public const int MaxTags = 10;
    private const int MinTagLength = 3;
    private const int MaxTagLength = 50;
    private const int ConcurrentChecks = 4;

    private static readonly string Instruction = EmbeddedText.Load("suggest-tags.txt");
    public static readonly IReadOnlyList<string> DefaultTags = ["digital_art", "made_with_ai"];
    private static readonly char[] Separators = ['\n', '\r', ','];
    private static readonly char[] Decoration = ['#', '"', '\'', '*', '`', '.'];

    public async Task<IReadOnlyList<string>> SuggestAsync(int setId, CancellationToken ct)
    {
        var prompt = await db.PromptSets.Where(s => s.Id == setId).Select(s => s.Prompt).SingleOrDefaultAsync(ct)
            ?? throw new UserFacingException("Set not found.");
        var key = await apiKey.GetAsync(ct) ?? throw new UserFacingException("Set the vision API key in Settings first.");

        VisionAnswer answer;
        try
        {
            answer = await vision.CompleteWithoutThinkingAsync(key, Instruction.Replace("{prompt}", prompt), ct);
        }
        catch (VisionException ex)
        {
            throw new UserFacingException(ex.Message);
        }
        return await KnownTagsAsync(Candidates(answer.Text), ct);
    }

    public static IReadOnlyList<string> Candidates(string answer) =>
        answer.Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => ListMarker().Replace(item.Trim(), "").Trim().Trim(Decoration).Trim())
            .Select(item => Whitespace().Replace(item, "_").ToLowerInvariant())
            .Where(tag => tag.Length is >= MinTagLength and <= MaxTagLength && TagPattern().IsMatch(tag) && !DefaultTags.Contains(tag))
            .Distinct()
            .ToList();

    private async Task<IReadOnlyList<string>> KnownTagsAsync(IReadOnlyList<string> candidates, CancellationToken ct)
    {
        var known = new List<string>();
        foreach (var batch in candidates.Chunk(ConcurrentChecks))
        {
            var checks = await Task.WhenAll(batch.Select(async tag => (Tag: tag, Known: await posting.IsKnownTagAsync(tag, ct))));
            known.AddRange(checks.Where(check => check.Known).Select(check => check.Tag));
            if (known.Count >= MaxTags)
                break;
        }
        return known.Take(MaxTags).ToList();
    }

    [GeneratedRegex(@"^(\d+[.)]|[-*•])\s*")]
    private static partial Regex ListMarker();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex("^[a-z0-9_]+$")]
    private static partial Regex TagPattern();
}
