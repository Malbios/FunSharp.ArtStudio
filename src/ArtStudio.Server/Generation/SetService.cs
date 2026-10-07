using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.Generation;

public abstract record SetSource
{
    public sealed record None : SetSource;

    public sealed record ImageFile(SourceKind Kind, Stream Content, string Extension) : SetSource;

    public sealed record CopyOf(int SetId) : SetSource;
}

public sealed record NewSetRequest(string Prompt, string Resolution, int Count, SetSource Source);

public sealed class SetService(StudioDbContext db, AppPaths paths, QueueService queueService, TimeProvider clock)
{
    public async Task<PromptSet> CreateAsync(NewSetRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new UserFacingException("The prompt must not be empty.");
        var resolution = Resolutions.Find(request.Resolution)
            ?? throw new UserFacingException($"Unknown resolution '{request.Resolution}'.");

        var set = new PromptSet
        {
            Prompt = request.Prompt.Trim(),
            Resolution = resolution.Name,
            SourceKind = SourceKind.None,
            CreatedAt = clock.GetUtcNow(),
        };

        if (request.Source is SetSource.CopyOf copyOf)
            await CopySourceAsync(set, copyOf.SetId, ct);

        db.PromptSets.Add(set);
        await db.SaveChangesAsync(ct);

        if (request.Source is SetSource.ImageFile file)
        {
            var settings = await SettingsStore.LoadAsync(db, paths, ct);
            set.SourceKind = file.Kind;
            set.SourceImagePath = await ImageStore.SaveSourceAsync(settings.OutputDirectory, set.Id, file.Content, file.Extension, ct);
            await db.SaveChangesAsync(ct);
        }

        await queueService.EnqueueAsync(set.Id, request.Count, ct);
        return set;
    }

    public async Task SelectImageAsync(int setId, int? imageId, CancellationToken ct)
    {
        var set = await db.PromptSets.SingleOrDefaultAsync(s => s.Id == setId, ct)
            ?? throw new UserFacingException("Set not found.");
        if (imageId is not null && !await db.Images.AnyAsync(i => i.Id == imageId && i.PromptSetId == setId, ct))
            throw new UserFacingException("That image does not belong to this set.");

        set.SelectedImageId = imageId;
        await db.SaveChangesAsync(ct);
    }

    private async Task CopySourceAsync(PromptSet target, int sourceSetId, CancellationToken ct)
    {
        var source = await db.PromptSets.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sourceSetId, ct)
            ?? throw new UserFacingException("The set to base this one on was not found.");

        target.SourceKind = source.SourceKind;
        target.SourceImagePath = source.SourceImagePath;
        target.DeviantArtUrl = source.DeviantArtUrl;
        target.DeviationId = source.DeviationId;
        target.DeviantArtAuthor = source.DeviantArtAuthor;
    }
}
