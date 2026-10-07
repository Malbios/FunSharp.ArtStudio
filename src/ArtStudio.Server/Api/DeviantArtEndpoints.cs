using ArtStudio.Server.Data;
using ArtStudio.Server.DeviantArt;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Generation;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.Api;

public sealed record DeviationPreviewRequest(string Url);

public sealed record BlockArtistRequest(string Username);

public sealed record BlockedArtistDto(int Id, string Username, DateTimeOffset CreatedAt);

public static class DeviantArtEndpoints
{
    public static void MapDeviantArtEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/deviantart/preview",
            (DeviationPreviewRequest request, DeviantArtService deviantArt, CancellationToken ct) =>
                deviantArt.PreviewAsync(request.Url, ct));

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

    private static async Task<BlockedArtistDto> BlockArtistAsync(
        BlockArtistRequest request, StudioDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var username = (DeviationUrl.UsernameFromProfileOrArtUrl(request.Username) ?? request.Username).Trim().TrimStart('@');
        if (username.Length == 0)
            throw new UserFacingException("Enter a DeviantArt username or profile URL.");
        if (await db.BlockedArtists.AnyAsync(a => a.Username == username, ct))
            throw new UserFacingException($"'{username}' is already blocked.");

        var artist = new BlockedArtist { Username = username, CreatedAt = clock.GetUtcNow() };
        db.BlockedArtists.Add(artist);
        await db.SaveChangesAsync(ct);
        return new BlockedArtistDto(artist.Id, artist.Username, artist.CreatedAt);
    }
}
