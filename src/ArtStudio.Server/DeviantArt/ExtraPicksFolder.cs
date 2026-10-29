using System.Diagnostics;
using ArtStudio.Server.Data;
using ArtStudio.Server.Generation;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.DeviantArt;

public interface IFolderOpener
{
    void Open(string folder);

    /// <summary>Closes the Explorer windows showing the folder, if any; never throws.</summary>
    void Close(string folder);
}

public sealed class ExplorerFolderOpener(ILogger<ExplorerFolderOpener> logger) : IFolderOpener
{
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);

    public void Open(string folder) => Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });

    public void Close(string folder)
    {
        if (!OperatingSystem.IsWindows())
            return;
        // Shell COM objects expect a single-threaded apartment, which request threads are not.
        var thread = new Thread(() => CloseWindowsShowing(Normalized(folder))) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(CloseTimeout))
            logger.LogWarning("Closing the Explorer window of {Folder} timed out", folder);
    }

    private void CloseWindowsShowing(string folder)
    {
        try
        {
            if (!OperatingSystem.IsWindows() || Type.GetTypeFromProgID("Shell.Application") is not { } shellType)
                return;
            dynamic shell = Activator.CreateInstance(shellType)!;
            foreach (var window in shell.Windows())
            {
                string? shown = null;
                try
                {
                    shown = window.Document.Folder.Self.Path;
                }
                catch (Exception)
                {
                    // Windows that show no folder, such as Internet Explorer remnants, have no path.
                }
                if (shown is not null && string.Equals(Normalized(shown), folder, StringComparison.OrdinalIgnoreCase))
                    window.Quit();
            }
        }
        catch (Exception failure)
        {
            logger.LogWarning(failure, "Could not close the Explorer window of {Folder}", folder);
        }
    }

    private static string Normalized(string folder) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
}

/// <summary>
/// DeviantArt's API takes one image per deviation, so picks #2 and up are added by hand in its editor. This puts
/// copies of them, numbered in pick order, into a folder of their own to drag from.
/// </summary>
public sealed record ExtraPick(int Position, int ImageId, string FilePath)
{
    /// <summary>The name the pick goes by in the folder and in DeviantArt's editor, so either can tell what is there.</summary>
    public string FileName => $"{Position:00}{Path.GetExtension(FilePath)}";
}

public sealed class ExtraPicksFolder(StudioDbContext db, AppPaths paths, IFolderOpener opener)
{
    /// <summary>Picks #2 and up, in pick order.</summary>
    public async Task<IReadOnlyList<ExtraPick>> ListAsync(int setId, CancellationToken ct) =>
        await db.Picks
            .Where(p => p.PromptSetId == setId)
            .OrderBy(p => p.Position)
            .Join(db.Images, p => p.GeneratedImageId, i => i.Id, (p, i) => new ExtraPick(p.Position, i.Id, i.FilePath))
            .Skip(1)
            .ToListAsync(ct);

    public async Task<string> OpenAsync(int setId, CancellationToken ct)
    {
        var files = await ListAsync(setId, ct);
        if (files.Count == 0)
            throw new UserFacingException("This set has no picks besides #1.");

        Delete(paths, opener, setId);
        var folder = Directory.CreateDirectory(FolderFor(paths, setId)).FullName;
        foreach (var file in files)
        {
            if (!File.Exists(file.FilePath))
                throw new UserFacingException($"The file of pick #{file.Position} is missing.");
            File.Copy(file.FilePath, Path.Combine(folder, file.FileName));
        }

        opener.Open(folder);
        return folder;
    }

    public void Close(int setId) => Delete(paths, opener, setId);

    /// <summary>Closes the folder's Explorer window too, so it is not left open on a folder that is gone.</summary>
    public static void Delete(AppPaths paths, IFolderOpener opener, int setId)
    {
        var folder = FolderFor(paths, setId);
        if (!Directory.Exists(folder))
            return;
        opener.Close(folder);
        Directory.Delete(folder, recursive: true);
    }

    private static string FolderFor(AppPaths paths, int setId) => Path.Combine(paths.ExtraPicksDirectory, $"set-{setId}");
}
