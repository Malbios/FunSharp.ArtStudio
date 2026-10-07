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
    ComfyClientFactory clientFactory,
    StudioNotifier notifier,
    TimeProvider clock,
    ILogger<QueueWorker> logger) : BackgroundService
{
    private static readonly TimeSpan ComfyCleanupTimeout = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeueInterruptedJobsAsync(stoppingToken);
        queue.Wake();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await queue.WaitForWakeAsync(stoppingToken);
                await DrainQueueAsync(stoppingToken);
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

    private async Task RequeueInterruptedJobsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
        await db.Jobs
            .Where(j => j.Status == JobStatus.Running)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, JobStatus.Queued)
                .SetProperty(j => j.CurrentComfyPromptId, (string?)null), ct);
    }

    private async Task DrainQueueAsync(CancellationToken stoppingToken)
    {
        var finishedAnyJob = false;
        while (true)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
            var settings = await SettingsStore.LoadAsync(db, scope.ServiceProvider.GetRequiredService<AppPaths>(), stoppingToken);
            if (settings.QueuePaused)
                return;

            var nextJobId = await db.Jobs
                .Where(j => j.Status == JobStatus.Queued)
                .OrderBy(j => j.QueuePosition)
                .Select(j => (int?)j.Id)
                .FirstOrDefaultAsync(stoppingToken);

            if (nextJobId is null)
            {
                if (finishedAnyJob)
                    await notifier.QueueEmpty();
                return;
            }

            await RunJobAsync(db, settings, nextJobId.Value, stoppingToken);
            finishedAnyJob = true;
        }
    }

    private async Task RunJobAsync(StudioDbContext db, StudioSettings settings, int jobId, CancellationToken stoppingToken)
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
            await notifier.JobUpdated(job.Id, job.PromptSetId);

            var client = clientFactory.Create(settings.ComfyServerUrl);
            try
            {
                await GenerateRemainingImagesAsync(db, client, settings, job, jobToken);
                await FinishAsync(db, job, JobStatus.Completed, error: null);
                await notifier.JobCompleted(job.Id, job.PromptSetId, job.PromptSet.Prompt);
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
                await FinishAsync(db, job, JobStatus.Failed, ex.Message);
                settings.QueuePaused = true;
                await db.SaveChangesAsync(CancellationToken.None);
                await notifier.QueueStateChanged(paused: true);
            }
        }
        finally
        {
            queue.EndRun();
        }
    }

    private async Task GenerateRemainingImagesAsync(
        StudioDbContext db, ComfyClient client, StudioSettings settings, GenerationJob job, CancellationToken ct)
    {
        var resolution = Resolutions.Find(job.PromptSet.Resolution)
            ?? throw new UserFacingException($"Unknown resolution '{job.PromptSet.Resolution}'.");

        while (job.RemainingCount > 0)
        {
            var seed = seeds.Next();
            var workflow = workflowBuilder.Build(job.PromptSet.Prompt, resolution, seed);

            var downloaded = await runner.RunAsync(client, workflow, async promptId =>
            {
                job.CurrentComfyPromptId = promptId;
                await db.SaveChangesAsync(ct);
            }, ct);

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
            await db.SaveChangesAsync(CancellationToken.None);

            foreach (var image in images)
                await notifier.ImageAdded(image.Id, job.PromptSetId);
            await notifier.JobUpdated(job.Id, job.PromptSetId);
        }
    }

    private async Task FinishAsync(StudioDbContext db, GenerationJob job, JobStatus status, string? error)
    {
        job.Status = status;
        job.Error = error;
        job.CurrentComfyPromptId = null;
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
