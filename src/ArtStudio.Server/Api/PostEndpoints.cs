using ArtStudio.Server.DeviantArt;

namespace ArtStudio.Server.Api;

public sealed record MatureGalleriesRequest(IReadOnlyList<string> FolderIds);

public static class PostEndpoints
{
    public static void MapPostEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/deviantart/galleries", (DeviantArtPosting posting, CancellationToken ct) => posting.GalleriesAsync(ct));
        app.MapPut("/api/deviantart/mature-galleries", async (MatureGalleriesRequest request, DeviantArtPosting posting, CancellationToken ct) =>
        {
            await posting.SaveMatureGalleriesAsync(request.FolderIds ?? [], ct);
            return await posting.GalleriesAsync(ct);
        });
    }
}
