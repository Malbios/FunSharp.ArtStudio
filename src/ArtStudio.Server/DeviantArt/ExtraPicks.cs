using ArtStudio.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.DeviantArt;

public sealed record ExtraPick(int Position, int ImageId, string FilePath)
{
    /// <summary>The name the pick goes by in DeviantArt's editor, so the browser extension can tell what is there.</summary>
    public string FileName => $"{Position:00}{Path.GetExtension(FilePath)}";
}

/// <summary>
/// DeviantArt's API takes one image per deviation, so picks #2 and up are added in its editor, by the browser
/// extension.
/// </summary>
public sealed class ExtraPicks(StudioDbContext db)
{
    /// <summary>Picks #2 and up, in pick order.</summary>
    public async Task<IReadOnlyList<ExtraPick>> ListAsync(int setId, CancellationToken ct) =>
        await db.Picks
            .Where(p => p.PromptSetId == setId)
            .OrderBy(p => p.Position)
            .Join(db.Images, p => p.GeneratedImageId, i => i.Id, (p, i) => new ExtraPick(p.Position, i.Id, i.FilePath))
            .Skip(1)
            .ToListAsync(ct);
}
