using System.Diagnostics;
using System.Text.Json.Nodes;

namespace ArtStudio.Server.Comfy;

public sealed record GenerationTiming(TimeSpan PollInterval, TimeSpan Timeout)
{
    public static readonly GenerationTiming Default = new(TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(10));
}

public sealed record DownloadedImage(string FileName, byte[] Content);

public sealed class GenerationRunner(GenerationTiming timing, ILogger<GenerationRunner> logger)
{
    public async Task<IReadOnlyList<DownloadedImage>> RunAsync(
        ComfyClient client, JsonObject workflow, Func<string, Task> onQueued, CancellationToken ct)
    {
        var queued = await client.QueuePromptAsync(workflow, ct);
        if (queued.NodeErrors is not null)
            logger.LogWarning("ComfyUI reported node errors for {PromptId}: {NodeErrors}", queued.PromptId, queued.NodeErrors);
        await onQueued(queued.PromptId);

        var status = await WaitForCompletionAsync(client, queued.PromptId, ct);

        var images = new List<DownloadedImage>();
        foreach (var image in status.Images)
            images.Add(new DownloadedImage(image.Filename, await client.DownloadImageAsync(image, ct)));

        if (images.Count == 0)
            throw new ComfyException("Workflow completed but returned no images. Ensure it includes a SaveImage or PreviewImage node.");
        return images;
    }

    private async Task<ComfyRunStatus> WaitForCompletionAsync(ComfyClient client, string promptId, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < timing.Timeout)
        {
            var status = await client.GetRunStatusAsync(promptId, ct);
            switch (status.State)
            {
                case ComfyRunState.Completed:
                    return status;
                case ComfyRunState.Failed:
                    throw new ComfyException(status.Error!);
            }
            await Task.Delay(timing.PollInterval, ct);
        }
        throw new ComfyException($"Timed out waiting for {promptId}. The server job may still be queued or running.");
    }
}
