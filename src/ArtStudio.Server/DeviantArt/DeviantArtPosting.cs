using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ArtStudio.Server.Data;
using ArtStudio.Server.Generation;
using ArtStudio.Server.Hubs;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.DeviantArt;

public sealed record Gallery(string FolderId, string Name, bool Mature, bool LastUsed);

public sealed record StashRequest(string Title, string? Description, string? Tags);

public sealed record PublishRequest(string GalleryId, bool IsMature, string? MatureLevel);

public sealed partial class DeviantArtPosting(
    StudioDbContext db,
    AppPaths paths,
    IHttpClientFactory httpClientFactory,
    DeviantArtAuth auth,
    StudioNotifier notifier,
    TimeProvider clock)
{
    public const string ApiBase = "https://www.deviantart.com/api/v1/oauth2";
    public const int MaxTitleLength = 50;
    private const int GalleryPageSize = 50;
    private const char IdSeparator = ',';
    private static readonly string?[] MatureLevels = ["moderate", "strict"];

    public async Task StashAsync(int setId, StashRequest request, CancellationToken ct)
    {
        var set = await db.PromptSets.SingleOrDefaultAsync(s => s.Id == setId, ct) ?? throw new UserFacingException("Set not found.");
        if (set.ReadyToPostAt is null || set.ArchivedAt is not null)
            throw new UserFacingException("Only sets in Post can be uploaded to DeviantArt.");
        if (set.StashItemId is not null)
            throw new UserFacingException("This set is already in your Stash.");

        var title = request.Title?.Trim() ?? "";
        if (title.Length == 0)
            throw new UserFacingException("Enter a title.");
        if (title.Length > MaxTitleLength)
            throw new UserFacingException($"The title must be at most {MaxTitleLength} characters.");
        var tags = ParseTags(request.Tags);
        // Not trimmed: a description ending in "Inspired by" keeps its final line break for the link to be pasted after.
        var description = string.IsNullOrWhiteSpace(request.Description) ? "" : request.Description;

        var mainPick = await db.Picks
            .Where(p => p.PromptSetId == setId)
            .OrderBy(p => p.Position)
            .Join(db.Images, p => p.GeneratedImageId, i => i.Id, (p, i) => i.FilePath)
            .FirstOrDefaultAsync(ct)
            ?? throw new UserFacingException("The set has no picked image.");
        if (!File.Exists(mainPick))
            throw new UserFacingException("The file of pick #1 is missing.");

        using var form = new MultipartFormDataContent { { new StringContent(title), "title" } };
        if (description.Length > 0)
            form.Add(new StringContent(DescriptionHtml(description)), "artist_comments");
        foreach (var tag in tags)
            form.Add(new StringContent(tag), "tags[]");
        form.Add(new StringContent("true"), "is_ai_generated");
        form.Add(new StringContent("false"), "noai");
        var file = new ByteArrayContent(await File.ReadAllBytesAsync(mainPick, ct));
        file.Headers.ContentType = new MediaTypeHeaderValue(ImageStore.ContentTypeFor(mainPick));
        form.Add(file, "file", Path.GetFileName(mainPick));

        var answer = await SendAsync(HttpMethod.Post, "stash/submit", form, ct);
        var itemId = answer["itemid"]?.GetValueKind() == JsonValueKind.Number ? answer["itemid"]!.GetValue<long>() : (long?)null;
        if (answer["status"]?.GetValue<string>() != "success" || itemId is null)
            throw new UserFacingException("DeviantArt did not confirm the Stash upload.");

        set.PostTitle = title;
        set.PostDescription = description;
        set.PostTags = string.Join(' ', tags);
        set.StashItemId = itemId;
        set.StashedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await notifier.SetUpdated(setId);
    }

    public async Task PublishAsync(int setId, PublishRequest request, CancellationToken ct)
    {
        var set = await db.PromptSets.SingleOrDefaultAsync(s => s.Id == setId, ct) ?? throw new UserFacingException("Set not found.");
        if (set.PublishedUrl is not null)
            throw new UserFacingException("This set is already published.");
        if (set.StashItemId is null || set.ArchivedAt is not null)
            throw new UserFacingException("Upload the set to your Stash first.");
        if (string.IsNullOrWhiteSpace(request.GalleryId))
            throw new UserFacingException("Choose a gallery.");
        if (request.IsMature && !MatureLevels.Contains(request.MatureLevel))
            throw new UserFacingException("Choose how mature the deviation is: moderate or strict.");

        List<KeyValuePair<string, string>> fields =
        [
            new("itemid", set.StashItemId.Value.ToString()),
            new("is_mature", Flag(request.IsMature)),
        ];
        if (request.IsMature)
            fields.Add(new("mature_level", request.MatureLevel!));
        fields.AddRange(
        [
            new("galleryids[]", request.GalleryId.Trim()),
            new("feature", "false"),
            new("allow_comments", "true"),
            new("display_resolution", "0"),
            new("license_options[creative_commons]", "true"),
            new("license_options[commercial]", "true"),
            new("license_options[modify]", "share"),
            new("allow_free_download", "true"),
            new("add_watermark", "false"),
        ]);
        fields.AddRange(ParseTags(set.PostTags).Select(tag => new KeyValuePair<string, string>("tags[]", tag)));
        fields.Add(new("is_ai_generated", "true"));
        fields.Add(new("noai", "false"));

        var answer = await SendAsync(HttpMethod.Post, "stash/publish", new FormUrlEncodedContent(fields), ct);
        var url = answer["url"]?.GetValue<string>();
        if (answer["status"]?.GetValue<string>() != "success" || url is null)
            throw new UserFacingException("DeviantArt did not confirm the publication. Check your gallery before trying again.");

        var now = clock.GetUtcNow();
        set.PublishedUrl = url;
        set.PublishedAt = now;
        set.ArchivedAt = now;
        (await SettingsStore.LoadAsync(db, paths, ct)).LastGalleryId = request.GalleryId.Trim();
        await db.SaveChangesAsync(ct);
        ExtraPicksFolder.Delete(paths, setId);
        await notifier.SetUpdated(setId);
    }

    private static string Flag(bool value) => value ? "true" : "false";

    public async Task ForgetStashAsync(int setId, CancellationToken ct)
    {
        var set = await db.PromptSets.SingleOrDefaultAsync(s => s.Id == setId, ct) ?? throw new UserFacingException("Set not found.");
        set.StashItemId = null;
        set.StashedAt = null;
        await db.SaveChangesAsync(ct);
        await notifier.SetUpdated(setId);
    }

    public static IReadOnlyList<string> ParseTags(string? tags)
    {
        var parsed = (tags ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.TrimStart('#'))
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var invalid = parsed.FirstOrDefault(tag => !TagPattern().IsMatch(tag));
        if (invalid is not null)
            throw new UserFacingException($"The tag '{invalid}' may only contain letters, digits and underscores.");
        return parsed;
    }

    [GeneratedRegex("^[A-Za-z0-9_]+$")]
    private static partial Regex TagPattern();

    // DeviantArt descriptions are HTML with <br /> line breaks; its editor could not read plain text line breaks.
    private static string DescriptionHtml(string description) =>
        WebUtility.HtmlEncode(description).ReplaceLineEndings("<br />");

    // The tag autocomplete misses common exact tags, so a tag counts as known when any deviation uses it.
    public async Task<bool> IsKnownTagAsync(string tag, CancellationToken ct)
    {
        var page = await SendAsync(HttpMethod.Get, $"browse/tags?tag={Uri.EscapeDataString(tag)}&limit=1&mature_content=true", null, ct);
        return page["results"]?.AsArray().Count > 0;
    }

    public async Task<IReadOnlyList<Gallery>> GalleriesAsync(CancellationToken ct)
    {
        var settings = await SettingsStore.LoadAsync(db, paths, ct);
        var matureIds = settings.MatureGalleryIds?.Split(IdSeparator).ToHashSet() ?? [];
        var galleries = new List<Gallery>();
        int? offset = 0;
        while (offset is not null)
        {
            var page = await SendAsync(HttpMethod.Get, $"gallery/folders?limit={GalleryPageSize}&offset={offset}", null, ct);
            foreach (var folder in page["results"]?.AsArray() ?? [])
            {
                var folderId = folder?["folderid"]?.GetValue<string>();
                var name = folder?["name"]?.GetValue<string>();
                if (folderId is not null && name is not null)
                    galleries.Add(new Gallery(folderId, name, matureIds.Contains(folderId), folderId == settings.LastGalleryId));
            }
            offset = page["has_more"]?.GetValue<bool>() == true ? page["next_offset"]?.GetValue<int>() : null;
        }
        return galleries;
    }

    public async Task SaveMatureGalleriesAsync(IReadOnlyList<string> folderIds, CancellationToken ct)
    {
        var settings = await SettingsStore.LoadAsync(db, paths, ct);
        var ids = folderIds.Select(id => id.Trim()).Where(id => id.Length > 0).Distinct().ToList();
        settings.MatureGalleryIds = ids.Count == 0 ? null : string.Join(IdSeparator, ids);
        await db.SaveChangesAsync(ct);
    }

    private async Task<JsonNode> SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, $"{ApiBase}/{path}") { Content = content };
        request.Headers.Authorization = new("Bearer", await auth.GetAccessTokenAsync(ct));
        using var response = await httpClientFactory.CreateClient(DeviantArtService.HttpClientName).SendAsync(request, ct);
        var body = ParseJsonOrNull(await response.Content.ReadAsStringAsync(ct));

        if (response.IsSuccessStatusCode && body is not null)
            return body;
        throw new UserFacingException(ErrorMessage(response.StatusCode, body));
    }

    private static string ErrorMessage(HttpStatusCode status, JsonNode? body)
    {
        var error = body?["error"]?.GetValue<string>();
        var description = body?["error_description"]?.GetValue<string>();
        return status switch
        {
            HttpStatusCode.Unauthorized => "DeviantArt rejected the login. Reconnect it in Settings.",
            HttpStatusCode.Forbidden when error == "insufficient_scope" || description?.Contains("scope") == true =>
                "The DeviantArt login does not allow Stash and publishing yet. Reconnect DeviantArt in Settings.",
            HttpStatusCode.TooManyRequests => "DeviantArt is limiting requests. Try again in a minute.",
            _ when description is not null => $"DeviantArt refused: {description}",
            _ when body is null => $"DeviantArt returned an unreadable response ({(int)status}).",
            _ => $"DeviantArt refused the request ({(int)status}).",
        };
    }

    private static JsonNode? ParseJsonOrNull(string text)
    {
        try
        {
            return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
