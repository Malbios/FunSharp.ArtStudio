using ArtStudio.Server.DeviantArt;
using ArtStudio.Server.Vision;

namespace ArtStudio.Server.Api;

public sealed record MatureGalleriesRequest(IReadOnlyList<string> FolderIds);

public sealed record SuggestedTitleDto(string Title);

public sealed record SuggestedTagsDto(IReadOnlyList<string> Tags);

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
        post.MapPost("suggest-title", async (int id, TitleSuggester titles, PostSuggestionTurns turns, CancellationToken ct) =>
        {
            using var turn = await turns.ClickedAsync(ct);
            return new SuggestedTitleDto(await titles.SuggestAsync(id, ct));
        });
        post.MapPost("suggest-tags", async (int id, TagSuggester tags, PostSuggestionTurns turns, CancellationToken ct) =>
        {
            using var turn = await turns.ClickedAsync(ct);
            return new SuggestedTagsDto(await tags.SuggestAsync(id, ct));
        });
        post.MapPost("stash", async (int id, StashRequest request, DeviantArtPosting posting, CancellationToken ct) =>
            await posting.StashAsync(id, request, ct));
        post.MapPost("publish", async (int id, PublishRequest request, DeviantArtPosting posting, CancellationToken ct) =>
            await posting.PublishAsync(id, request, ct));
        post.MapPost("forget-stash", async (int id, DeviantArtPosting posting, CancellationToken ct) =>
            await posting.ForgetStashAsync(id, ct));
        post.MapPost("extra-picks", async (int id, ExtraPicksFolder folder, CancellationToken ct) =>
            new { Folder = await folder.OpenAsync(id, ct) });
    }
}
