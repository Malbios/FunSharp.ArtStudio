using ArtStudio.Server.Data;
using ArtStudio.Server.DeviantArt;
using ArtStudio.Server.Generation;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.Vision;

public sealed class TitleSuggester(StudioDbContext db, VisionClient vision, VisionApiKey apiKey)
{
    private const int MaxTitleLength = DeviantArtPosting.MaxTitleLength;

    private static readonly string Instruction = EmbeddedText.Load("suggest-title.txt");
    private static readonly char[] Quotes = ['"', '\'', '“', '”', '‘', '’', '*'];

    public async Task<string> SuggestAsync(int setId, CancellationToken ct)
    {
        var prompt = await db.PromptSets.Where(s => s.Id == setId).Select(s => s.Prompt).SingleOrDefaultAsync(ct)
            ?? throw new UserFacingException("Set not found.");
        var key = await apiKey.GetAsync(ct) ?? throw new UserFacingException("Set the vision API key in Settings first.");

        VisionAnswer answer;
        try
        {
            answer = await vision.CompleteAsync(key, Instruction.Replace("{prompt}", prompt), ct);
        }
        catch (VisionException ex)
        {
            throw new UserFacingException(ex.Message);
        }
        return Clean(answer.Text);
    }

    public static string Clean(string answer)
    {
        var line = answer.Split('\n').Select(l => l.Trim().Trim(Quotes).Trim()).FirstOrDefault(l => l.Length > 0) ?? "";
        if (line.StartsWith("Title:", StringComparison.OrdinalIgnoreCase))
            line = line["Title:".Length..].Trim().Trim(Quotes).Trim();
        if (line.Length <= MaxTitleLength)
            return line;

        var cut = line[..MaxTitleLength];
        var lastSpace = cut.LastIndexOf(' ');
        return (lastSpace > 0 ? cut[..lastSpace] : cut).TrimEnd(',', ';', ':', '-', ' ');
    }
}
