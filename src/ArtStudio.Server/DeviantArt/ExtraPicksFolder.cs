using System.Diagnostics;
using ArtStudio.Server.Data;
using ArtStudio.Server.Generation;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.DeviantArt;

public interface IFolderOpener
{
    void Open(string folder);
}

public sealed class ExplorerFolderOpener : IFolderOpener
{
    public void Open(string folder) => Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
}

/// <summary>
/// DeviantArt's API takes one image per deviation, so picks #2 and up are added by hand in its editor. This puts
/// copies of them, numbered in pick order, into a folder of their own to drag from.
/// </summary>
public sealed class ExtraPicksFolder(StudioDbContext db, AppPaths paths, IFolderOpener opener)
{
    public async Task<string> OpenAsync(int setId, CancellationToken ct)
    {
        var files = await db.Picks
            .Where(p => p.PromptSetId == setId)
            .OrderBy(p => p.Position)
            .Join(db.Images, p => p.GeneratedImageId, i => i.Id, (p, i) => new { p.Position, i.FilePath })
            .Skip(1)
            .ToListAsync(ct);
        if (files.Count == 0)
            throw new UserFacingException("This set has no picks besides #1.");

        Delete(paths, setId);
        var folder = Directory.CreateDirectory(FolderFor(paths, setId)).FullName;
        foreach (var file in files)
        {
            if (!File.Exists(file.FilePath))
                throw new UserFacingException($"The file of pick #{file.Position} is missing.");
            File.Copy(file.FilePath, Path.Combine(folder, $"{file.Position:00}{Path.GetExtension(file.FilePath)}"));
        }

        opener.Open(folder);
        return folder;
    }

    public static void Delete(AppPaths paths, int setId)
    {
        var folder = FolderFor(paths, setId);
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
    }

    private static string FolderFor(AppPaths paths, int setId) => Path.Combine(paths.ExtraPicksDirectory, $"set-{setId}");
}
