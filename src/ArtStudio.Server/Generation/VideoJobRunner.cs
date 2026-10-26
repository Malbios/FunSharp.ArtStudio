using ArtStudio.Server.Comfy;
using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Google;
using ArtStudio.Server.Hubs;

namespace ArtStudio.Server.Generation;

public sealed record VideoTiming(TimeSpan RenderTimeout, TimeSpan DriveTimeout)
{
    public static readonly VideoTiming Default = new(TimeSpan.FromHours(3), TimeSpan.FromMinutes(15));
}

/// <summary>
/// Renders a video job's videos on ComfyUI and downloads each one from ComfyUI's output folder on Google Drive, so
/// videos never travel through the ngrok tunnel.
/// </summary>
public sealed class VideoJobRunner(
    VideoWorkflowBuilder workflowBuilder,
    GenerationRunner runner,
    GenerationTiming generationTiming,
    VideoTiming videoTiming,
    GoogleDrive drive,
    SeedGenerator seeds,
    StudioNotifier notifier,
    TimeProvider clock,
    ILogger<VideoJobRunner> logger)
{
    public async Task GenerateRemainingVideosAsync(
        StudioDbContext db, ComfyClient client, StudioSettings settings, GenerationJob job, CancellationToken ct)
    {
        await CollectPendingVideoAsync(db, client, settings, job, ct);

        while (job.RemainingCount > 0)
        {
            var seed = seeds.Next();
            var workflow = workflowBuilder.Build(new VideoWorkflowInputs(
                await client.UploadImageAsync(job.VideoStartImagePath!, $"artstudio-{Guid.NewGuid():N}", ct),
                job.VideoEndImagePath is null ? null : await client.UploadImageAsync(job.VideoEndImagePath, $"artstudio-{Guid.NewGuid():N}", ct),
                job.Prompt,
                job.VideoSeconds!.Value,
                job.VideoMegapixels!.Value,
                job.VideoTurbo,
                seed,
                // Unique, so a fresh ComfyUI whose file counter starts over can never overwrite an earlier video.
                $"video/artstudio-{Guid.NewGuid():N}"));

            var outputs = await runner.RunForOutputsAsync(client, workflow, async promptId =>
            {
                job.CurrentComfyPromptId = promptId;
                job.CurrentSeed = seed;
                job.CurrentRunStartedAt = clock.GetUtcNow();
                await db.SaveChangesAsync(ct);
                await notifier.JobUpdated(job.Id, job.PromptSetId);
            }, videoTiming.RenderTimeout, ct);
            var renderSeconds = (clock.GetUtcNow() - job.CurrentRunStartedAt!.Value).TotalSeconds;

            await RememberOutputAsync(db, job, outputs, ct);
            await DownloadFromDriveAsync(db, settings, job, renderSeconds, ct);
        }
    }

    /// <summary>Finishes a video that was rendering or waiting for Drive before a restart or a failure.</summary>
    private async Task CollectPendingVideoAsync(
        StudioDbContext db, ComfyClient client, StudioSettings settings, GenerationJob job, CancellationToken ct)
    {
        if (job.CurrentOutputFileName is null && job.CurrentComfyPromptId is { } promptId && job.CurrentSeed is not null)
        {
            var outputs = await runner.ResumeForOutputsAsync(client, promptId, videoTiming.RenderTimeout, ct);
            if (outputs is null)
            {
                logger.LogInformation("ComfyUI no longer knows run {PromptId} of job {JobId}; rendering that video again", promptId, job.Id);
                job.CurrentComfyPromptId = null;
                job.CurrentSeed = null;
                await db.SaveChangesAsync(ct);
                return;
            }
            await RememberOutputAsync(db, job, outputs, ct);
        }

        // The render spanned a restart or a failure, so how long it took is unknown.
        if (job.CurrentOutputFileName is not null && job.CurrentSeed is not null)
            await DownloadFromDriveAsync(db, settings, job, renderSeconds: null, ct);
    }

    private static async Task RememberOutputAsync(
        StudioDbContext db, GenerationJob job, IReadOnlyList<ComfyImageRef> outputs, CancellationToken ct)
    {
        var video = outputs.FirstOrDefault(o => o.Type == "output")
            ?? throw new ComfyException("The video workflow completed but saved no file. Ensure it includes its SaveVideo node.");
        job.CurrentOutputFileName = string.IsNullOrEmpty(video.Subfolder) ? video.Filename : $"{video.Subfolder}/{video.Filename}";
        job.CurrentComfyPromptId = null;
        job.CurrentRunStartedAt = null;
        await db.SaveChangesAsync(ct);
    }

    private async Task DownloadFromDriveAsync(
        StudioDbContext db, StudioSettings settings, GenerationJob job, double? renderSeconds, CancellationToken ct)
    {
        var outputPath = job.CurrentOutputFileName!;
        var fileName = Path.GetFileName(outputPath);
        var folderPath = $"{settings.GoogleDriveOutputFolder}/{Path.GetDirectoryName(outputPath)?.Replace('\\', '/')}".TrimEnd('/');

        var file = await WaitForDriveFileAsync(folderPath, fileName, ct);
        using var content = new MemoryStream();
        await drive.DownloadAsync(file, content, ct);

        var seed = job.CurrentSeed!.Value;
        var path = await ImageStore.SaveGeneratedAsync(
            settings.OutputDirectory, job.PromptSetId, seed, fileName, content.ToArray(), CancellationToken.None);
        db.Videos.Add(new GeneratedVideo
        {
            PromptSetId = job.PromptSetId,
            GenerationJobId = job.Id,
            FilePath = path,
            Seed = seed,
            CreatedAt = clock.GetUtcNow(),
            RenderSeconds = renderSeconds,
        });
        job.CompletedCount++;
        job.CurrentOutputFileName = null;
        job.CurrentSeed = null;
        await db.SaveChangesAsync(CancellationToken.None);
        await notifier.JobUpdated(job.Id, job.PromptSetId);
    }

    // Colab writes into Drive through its mount, so the file (and, for the very first video, its folder) shows up
    // there a little after ComfyUI reports it done.
    private async Task<DriveFile> WaitForDriveFileAsync(string folderPath, string fileName, CancellationToken ct)
    {
        var deadline = clock.GetUtcNow() + videoTiming.DriveTimeout;
        string? folderId = null;
        while (clock.GetUtcNow() < deadline)
        {
            folderId ??= await drive.TryFindFolderAsync(folderPath, ct);
            if (folderId is not null && await drive.FindCompleteFileAsync(folderId, fileName, ct) is { } file)
                return file;
            await Task.Delay(generationTiming.PollInterval, ct);
        }
        throw new UserFacingException(
            $"{fileName} did not appear in the Google Drive folder '{folderPath}' within {videoTiming.DriveTimeout.TotalMinutes:0} minutes. " +
            "Check that ComfyUI's output folder is on Drive and set in Settings, then retry the job.");
    }
}
