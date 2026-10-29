using ArtStudio.Server.Data;
using ArtStudio.Server.DeviantArt;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Generation;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.Api;

public sealed record DeviationPreviewRequest(string Url);

public sealed record BlockArtistRequest(string Username);

public sealed record BlockedArtistDto(int Id, string Username, DateTimeOffset CreatedAt);

/// <summary>PostedUrls are the archived sets already on DeviantArt, whose API cannot unpublish them.</summary>
public sealed record BlockArtistResultDto(BlockedArtistDto Artist, int ArchivedSets, int DeletedDrafts, IReadOnlyList<string> PostedUrls);

/// <summary>
/// What the browser extension needs to tell from a link alone whether a post would be refused, and whether its user
/// already inspired a set (PublishedUsers: one of those sets is on DeviantArt).
/// </summary>
public sealed record KnownDeviationsDto(
    IReadOnlyDictionary<string, int> UsedDeviations, IReadOnlyList<string> BlockedUsers, string? OwnUsername,
    IReadOnlyList<string> InspiringUsers, IReadOnlyList<string> PublishedUsers);

public static class DeviantArtEndpoints
{
    public static void MapDeviantArtEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/deviantart/preview",
            (DeviationPreviewRequest request, DeviantArtService deviantArt, CancellationToken ct) =>
                deviantArt.PreviewAsync(request.Url, ct));
        app.MapGet("/api/deviantart/known", GetKnownDeviationsAsync);

        var blocked = app.MapGroup("/api/blocked-artists");
        blocked.MapGet("", async (StudioDbContext db, CancellationToken ct) =>
            await db.BlockedArtists
                .OrderBy(a => a.Username)
                .Select(a => new BlockedArtistDto(a.Id, a.Username, a.CreatedAt))
                .ToListAsync(ct));
        blocked.MapPost("", BlockArtistAsync);
        blocked.MapDelete("{id:int}", async (int id, StudioDbContext db, CancellationToken ct) =>
        {
            await db.BlockedArtists.Where(a => a.Id == id).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        });
    }

    private static async Task<KnownDeviationsDto> GetKnownDeviationsAsync(
        StudioDbContext db, DeviantArtAuth auth, CancellationToken ct)
    {
        // The lowest set id, like the "already used in set #N" check names.
        var used = await db.PromptSets
            .Where(s => s.DeviationId != null)
            .GroupBy(s => s.DeviationId!)
            .Select(g => new { DeviationId = g.Key, SetId = g.Min(s => s.Id) })
            .ToDictionaryAsync(d => d.DeviationId, d => d.SetId, ct);
        var blocked = await db.BlockedArtists.Select(a => a.Username).ToListAsync(ct);
        // Grouped here: SQLite would scan all sets again for every author to tell whether one was published.
        var authors = (await db.PromptSets
                .Where(s => s.DeviantArtAuthor != null && s.DeviantArtAuthor != "")
                .Select(s => new { Username = s.DeviantArtAuthor!, Published = s.PublishedAt != null })
                .ToListAsync(ct))
            .GroupBy(s => s.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new KnownDeviationsDto(
            used, blocked, (await auth.GetStatusAsync(ct)).Username,
            authors.Select(g => g.Key).ToList(),
            authors.Where(g => g.Any(s => s.Published)).Select(g => g.Key).ToList());
    }

    private static async Task<BlockArtistResultDto> BlockArtistAsync(
        BlockArtistRequest request, StudioDbContext db, SetService setService, TimeProvider clock, CancellationToken ct)
    {
        var username = (DeviationUrl.UsernameFromProfileOrArtUrl(request.Username) ?? request.Username).Trim().TrimStart('@');
        if (username.Length == 0)
            throw new UserFacingException("Enter a DeviantArt username or profile URL.");
        if (await db.BlockedArtists.AnyAsync(a => a.Username == username, ct))
            throw new UserFacingException($"'{username}' is already blocked.");

        var artist = new BlockedArtist { Username = username, CreatedAt = clock.GetUtcNow() };
        db.BlockedArtists.Add(artist);
        await db.SaveChangesAsync(ct);
        var archived = await setService.ArchiveSetsOfArtistAsync(artist.Username, ct);
        return new BlockArtistResultDto(
            new BlockedArtistDto(artist.Id, artist.Username, artist.CreatedAt),
            archived.ArchivedCount, archived.DeletedDraftCount, archived.PostedUrls);
    }
}
