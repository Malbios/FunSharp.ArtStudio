using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Generation;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.Google;

public sealed record OutputFolderContents(string Folder, int FileCount, long TotalBytes);

/// <summary>ComfyUI's output folder on Google Drive, where every rendered image and video accumulates.</summary>
public sealed class ComfyOutputFolder(StudioDbContext db, AppPaths paths, GoogleDrive drive)
{
    public async Task<OutputFolderContents> MeasureAsync(CancellationToken ct)
    {
        var (folder, files) = await ListAsync(ct);
        return new OutputFolderContents(folder, files.Count, files.Sum(f => f.Size));
    }

    /// <summary>
    /// Moves every file in the folder and its subfolders to Drive's trash. Refused while ComfyUI may still be writing
    /// or Art Studio still has to download something from there.
    /// </summary>
    public async Task<OutputFolderContents> ClearAsync(CancellationToken ct)
    {
        if (await db.Jobs.AnyAsync(j => j.Status == JobStatus.Running, ct))
            throw new UserFacingException("A job is running. Wait until it is done or pause the queue, then clear the folder.");
        if (await db.Jobs.AnyAsync(j => j.CurrentOutputFileName != null, ct))
            throw new UserFacingException(
                "A finished video still has to be downloaded from Drive. Retry or cancel its job first, then clear the folder.");

        var (folder, files) = await ListAsync(ct);
        foreach (var file in files)
            await drive.TrashAsync(file, ct);
        return new OutputFolderContents(folder, files.Count, files.Sum(f => f.Size));
    }

    private async Task<(string Folder, IReadOnlyList<DriveFile> Files)> ListAsync(CancellationToken ct)
    {
        var folder = (await SettingsStore.LoadAsync(db, paths, ct)).GoogleDriveOutputFolder;
        var folderId = await drive.FindFolderAsync(folder, ct);
        return (folder, await drive.ListFilesRecursivelyAsync(folderId, ct));
    }
}
