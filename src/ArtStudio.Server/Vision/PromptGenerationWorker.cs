using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Generation;
using ArtStudio.Server.Hubs;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server.Vision;

public sealed class PromptGenerationWorker(
    IServiceScopeFactory scopeFactory,
    PromptGenerationQueue queue,
    VisionClient visionClient,
    VisionApiKey apiKey,
    VisionInstruction instruction,
    StudioNotifier notifier,
    ILogger<PromptGenerationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeueInterruptedAsync(stoppingToken);
        queue.Wake();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await queue.WaitForWakeAsync(stoppingToken);
                await DrainAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Prompt generation loop failed");
            }
        }
    }

    private async Task RequeueInterruptedAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<StudioDbContext>().PromptSets
            .Where(s => s.PromptGeneration == PromptGenerationState.Running)
            .ExecuteUpdateAsync(s => s.SetProperty(set => set.PromptGeneration, PromptGenerationState.Queued), ct);
    }

    private async Task DrainAsync(CancellationToken stoppingToken)
    {
        while (await NextQueuedDraftAsync(stoppingToken) is { } setId)
            await GenerateAsync(setId, stoppingToken);
    }

    private async Task<int?> NextQueuedDraftAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<StudioDbContext>().PromptSets
            .Where(s => s.PromptGeneration == PromptGenerationState.Queued)
            .OrderBy(s => s.PromptGenerationQueuedAt)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(ct);
    }

    private async Task GenerateAsync(int setId, CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
        // Registered before the set shows as running, so a cancel right after the state change is never missed.
        var runToken = queue.BeginRun(setId, stoppingToken);
        try
        {
            var claimed = await db.PromptSets
                .Where(s => s.Id == setId && s.PromptGeneration == PromptGenerationState.Queued)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(set => set.PromptGeneration, PromptGenerationState.Running)
                    .SetProperty(set => set.PromptGenerationError, (string?)null), stoppingToken);
            if (claimed == 0)
                return;
            await notifier.SetUpdated(setId);

            var answer = await DescribeSourceImageAsync(db, setId, runToken);
            var prompt = PromptCleaner.Clean(answer.Text);
            await FinishRunningAsync(db, setId, s => s
                .SetProperty(set => set.GeneratedPrompt, prompt)
                .SetProperty(set => set.Prompt, prompt)
                .SetProperty(set => set.PromptGeneration, PromptGenerationState.Done)
                .SetProperty(set => set.PromptGenerationTruncated, answer.Truncated));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Prompt generation for set {SetId} was cancelled", setId);
        }
        catch (Exception ex)
        {
            if (ex is not (VisionException or UserFacingException))
                logger.LogError(ex, "Prompt generation for set {SetId} failed", setId);
            await FinishRunningAsync(db, setId, s => s
                .SetProperty(set => set.PromptGeneration, PromptGenerationState.Failed)
                .SetProperty(set => set.PromptGenerationError, ex.Message));
        }
        finally
        {
            queue.EndRun();
        }
    }

    private async Task<VisionAnswer> DescribeSourceImageAsync(StudioDbContext db, int setId, CancellationToken ct)
    {
        var key = await apiKey.GetAsync(ct)
            ?? throw new UserFacingException("Set the vision API key in Settings.");
        var imagePath = await db.PromptSets.Where(s => s.Id == setId).Select(s => s.SourceImagePath).SingleAsync(ct);
        if (imagePath is null || !File.Exists(imagePath))
            throw new UserFacingException("The draft's image file is missing.");

        var image = await File.ReadAllBytesAsync(imagePath, ct);
        return await visionClient.DescribeAsync(key, image, ImageStore.ContentTypeFor(imagePath), instruction.Text, ct);
    }

    private async Task FinishRunningAsync(
        StudioDbContext db,
        int setId,
        Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<PromptSet>> setters)
    {
        await db.PromptSets
            .Where(s => s.Id == setId && s.PromptGeneration == PromptGenerationState.Running)
            .ExecuteUpdateAsync(setters, CancellationToken.None);
        await notifier.SetUpdated(setId);
    }
}
