using ArtStudio.Server.DeviantArt;
using ArtStudio.Server.Vision;

namespace ArtStudio.Server.Api;

public sealed record MatureGalleriesRequest(IReadOnlyList<string> FolderIds);

public sealed record SuggestedTitleDto(string Title);

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

        var post = app.MapGroup("/api/sets/{id:int}/post");
        post.MapPost("suggest-title", async (int id, TitleSuggester titles, CancellationToken ct) =>
            new SuggestedTitleDto(await titles.SuggestAsync(id, ct)));
    }
}
