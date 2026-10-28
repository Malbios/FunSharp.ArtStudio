using ArtStudio.Server.Comfy;
using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Hubs;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.Generation;

public sealed class QueueWorker(
    IServiceScopeFactory scopeFactory,
    GenerationQueue queue,
    ComfyWorkflowBuilder workflowBuilder,
    SeedGenerator seeds,
    GenerationRunner runner,
    VideoJobRunner videoRunner,
    ComfyClientFactory clientFactory,
    StudioNotifier notifier,
    TimeProvider clock,
    ILogger<QueueWorker> logger) : BackgroundService
{
    private static readonly TimeSpan ComfyCleanupTimeout = TimeSpan.FromSeconds(10);

    private sealed record ServerRun(int JobId, Task Run);

    private readonly Lock _lock = new();
    private readonly Dictionary<string, ServerRun> _runsByServerUrl = [];
    private bool _finishedAnyJob;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeueInterruptedJobsAsync(stoppingToken);
        queue.Wake();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await queue.WaitForWakeAsync(stoppingToken);
                    await StartWaitingJobsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Queue worker loop failed");
                }
            }
        }
        finally
        {
            Task[] runs;
            lock (_lock)
                runs = [.. _runsByServerUrl.Values.Select(run => run.Run)];
            await Task.WhenAll(runs);
        }
    }

    private async Task RequeueInterruptedJobsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
        // The run id and seed are kept so the image ComfyUI was rendering can still be collected.
        await db.Jobs
            .Where(j => j.Status == JobStatus.Running)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, JobStatus.Queued), ct);
    }

    /// <summary>
    /// Starts the next waiting job on every idle ComfyUI server. Images and videos run side by side on servers of their
    /// own; a server that renders both runs one job at a time.
    /// </summary>
    private async Task StartWaitingJobsAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
        var settings = await SettingsStore.LoadAsync(db, scope.ServiceProvider.GetRequiredService<AppPaths>(), stoppingToken);
        // Pausing one kind leaves the other running, without ever switching ComfyUI to the paused kind's model.
        var waiting = (await db.Jobs
                .Where(j => j.Status == JobStatus.Queued)
                .Select(j => new { j.Id, j.Kind, j.QueuePosition })
                .ToListAsync(stoppingToken))
            .Where(j => !settings.IsPaused(j.Kind))
            .ToList();

        bool queueEmpty;
        lock (_lock)
        {
            foreach (var server in Enum.GetValues<JobKind>().GroupBy(settings.ServerUrlFor))
            {
                if (_runsByServerUrl.ContainsKey(server.Key))
                    continue;
                var startedJobIds = _runsByServerUrl.Values.Select(run => run.JobId).ToHashSet();
                var next = queue.InRunOrder(
                        waiting.Where(j => server.Contains(j.Kind) && !startedJobIds.Contains(j.Id)), j => j.Kind, j => j.QueuePosition)
                    .FirstOrDefault();
                if (next is null)
                    continue;
                var serverUrl = server.Key;
                _runsByServerUrl[serverUrl] = new ServerRun(next.Id, Task.Run(() => RunOnServerAsync(serverUrl, next.Id, stoppingToken)));
            }

            queueEmpty = _runsByServerUrl.Count == 0 && _finishedAnyJob;
            if (queueEmpty)
                _finishedAnyJob = false;
        }

        if (queueEmpty)
            await notifier.QueueEmpty();
    }

    private async Task RunOnServerAsync(string serverUrl, int jobId, CancellationToken stoppingToken)
    {
        var finished = false;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
            var settings = await SettingsStore.LoadAsync(db, scope.ServiceProvider.GetRequiredService<AppPaths>(), stoppingToken);
            await RunJobAsync(db, settings, jobId, serverUrl, stoppingToken);
            finished = true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Queue worker failed to run job {JobId}", jobId);
        }
        finally
        {
            lock (_lock)
            {
                _runsByServerUrl.Remove(serverUrl);
                _finishedAnyJob |= finished;
            }
        }

        if (finished)
            queue.Wake();
    }

    private async Task RunJobAsync(
        StudioDbContext db, StudioSettings settings, int jobId, string serverUrl, CancellationToken stoppingToken)
    {
        var jobToken = queue.BeginRun(jobId, stoppingToken);
        try
        {
            var claimed = await db.Jobs
                .Where(j => j.Id == jobId && j.Status == JobStatus.Queued)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, JobStatus.Running)
                    .SetProperty(j => j.StartedAt, clock.GetUtcNow()), stoppingToken);
            if (claimed == 0)
                return;

            var job = await db.Jobs.Include(j => j.PromptSet).SingleAsync(j => j.Id == jobId, stoppingToken);
            queue.LoadedKind = job.Kind;
            await notifier.JobUpdated(job.Id, job.PromptSetId);

            var client = clientFactory.Create(serverUrl);
            try
            {
                if (job.Kind == JobKind.Video)
                    await videoRunner.GenerateRemainingVideosAsync(db, client, settings, job, jobToken);
                else
                    await GenerateRemainingImagesAsync(db, client, settings, job, jobToken);
                await FinishAsync(db, job, JobStatus.Completed, error: null);
                await notifier.JobCompleted(job.Id, job.PromptSetId, job.Prompt);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                await CleanUpComfyAsync(client, job.CurrentComfyPromptId);
                await FinishAsync(db, job, JobStatus.Cancelled, error: null);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Job {JobId} failed", job.Id);
                // The settings may have changed while the job ran, e.g. the other kind was paused.
                await db.Entry(settings).ReloadAsync(CancellationToken.None);
                settings.SetPaused(job.Kind, paused: true);
                await FinishAsync(db, job, JobStatus.Failed, ex.Message);
                await notifier.QueueStateChanged(settings.ImageQueuePaused, settings.VideoQueuePaused);
            }
        }
        finally
        {
            queue.EndRun(jobId);
        }
    }

    private async Task GenerateRemainingImagesAsync(
        StudioDbContext db, ComfyClient client, StudioSettings settings, GenerationJob job, CancellationToken ct)
    {
        var resolution = Resolutions.Find(job.Resolution)
            ?? throw new UserFacingException($"Unknown resolution '{job.Resolution}'.");

        await CollectRunFromBeforeRestartAsync(db, client, settings, job, ct);

        while (job.RemainingCount > 0)
        {
            var seed = seeds.Next();
            var workflow = workflowBuilder.Build(ParagraphOrder.Apply(job.Prompt, job.ParagraphOrder), resolution, seed);

            var downloaded = await runner.RunAsync(client, workflow, async promptId =>
            {
                job.CurrentComfyPromptId = promptId;
                job.CurrentSeed = seed;
                await db.SaveChangesAsync(ct);
            }, ct);

            await SaveImagesAsync(db, settings, job, seed, downloaded);
        }
    }

    private async Task CollectRunFromBeforeRestartAsync(
        StudioDbContext db, ComfyClient client, StudioSettings settings, GenerationJob job, CancellationToken ct)
    {
        if (job.CurrentComfyPromptId is not { } promptId)
            return;

        var downloaded = job.CurrentSeed is null ? null : await runner.ResumeAsync(client, promptId, ct);
        if (downloaded is null)
        {
            logger.LogInformation("ComfyUI no longer knows run {PromptId} of job {JobId}; rendering that image again", promptId, job.Id);
            job.CurrentComfyPromptId = null;
            job.CurrentSeed = null;
            await db.SaveChangesAsync(ct);
            return;
        }

        await SaveImagesAsync(db, settings, job, job.CurrentSeed!.Value, downloaded);
    }

    private async Task SaveImagesAsync(
        StudioDbContext db, StudioSettings settings, GenerationJob job, long seed, IReadOnlyList<DownloadedImage> downloaded)
    {
        var images = new List<GeneratedImage>();
        foreach (var image in downloaded)
        {
            var path = await ImageStore.SaveGeneratedAsync(
                settings.OutputDirectory, job.PromptSetId, seed, image.FileName, image.Content, CancellationToken.None);
            images.Add(new GeneratedImage
            {
                PromptSetId = job.PromptSetId,
                GenerationJobId = job.Id,
                FilePath = path,
                Seed = seed,
                CreatedAt = clock.GetUtcNow(),
            });
        }
        db.Images.AddRange(images);
        job.CompletedCount++;
        job.CurrentComfyPromptId = null;
        job.CurrentSeed = null;
        await db.SaveChangesAsync(CancellationToken.None);

        foreach (var image in images)
            await notifier.ImageAdded(image.Id, job.PromptSetId);
        await notifier.JobUpdated(job.Id, job.PromptSetId);
    }

    private async Task FinishAsync(StudioDbContext db, GenerationJob job, JobStatus status, string? error)
    {
        job.Status = status;
        job.Error = error;
        job.CurrentComfyPromptId = null;
        job.CurrentRunStartedAt = null;
        // A rendered video that could not be fetched from Drive (e.g. an expired Google login) is fetched on retry
        // instead of being rendered again.
        if (status != JobStatus.Failed || job.CurrentOutputFileName is null)
        {
            job.CurrentSeed = null;
            job.CurrentOutputFileName = null;
        }
        job.FinishedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(CancellationToken.None);
        await notifier.JobUpdated(job.Id, job.PromptSetId);
    }

    private async Task CleanUpComfyAsync(ComfyClient client, string? promptId)
    {
        if (promptId is null)
            return;
        using var timeout = new CancellationTokenSource(ComfyCleanupTimeout);
        try
        {
            await client.DeleteFromQueueAsync(promptId, timeout.Token);
            if (await client.IsRunningAsync(promptId, timeout.Token))
                await client.InterruptAsync(timeout.Token);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not cancel ComfyUI prompt {PromptId}", promptId);
        }
    }
}
