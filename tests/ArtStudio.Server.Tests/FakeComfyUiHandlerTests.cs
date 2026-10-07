using System.Text.Json.Nodes;
using ArtStudio.Server.Comfy;
using ArtStudio.Server.Generation;
using ArtStudio.Server.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArtStudio.Server.Tests;

public class FakeComfyUiHandlerTests
{
    private static readonly GenerationTiming FastTiming = new(TimeSpan.FromMilliseconds(5), TimeSpan.FromSeconds(5));

    private static ComfyClient ClientFor(FakeComfyUiHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://fake-comfy/") });

    private static JsonObject Workflow(string resolution) =>
        ComfyWorkflowBuilder.FromEmbeddedTemplate().Build("a fox", Resolutions.Find(resolution)!, 42);

    [Fact]
    public async Task Run_ProducesPngAtRequestedResolution()
    {
        var handler = new FakeComfyUiHandler(TimeSpan.Zero, TimeProvider.System);
        var runner = new GenerationRunner(FastTiming, NullLogger<GenerationRunner>.Instance);

        var image = Assert.Single(await runner.RunAsync(ClientFor(handler), Workflow("Wide"), _ => Task.CompletedTask, CancellationToken.None));

        Assert.Equal([0x89, 0x50, 0x4E, 0x47], image.Content.Take(4));
        Assert.Equal(1344, ReadBigEndian(image.Content, 16));
        Assert.Equal(768, ReadBigEndian(image.Content, 20));
    }

    [Fact]
    public async Task Interrupt_FailsTheRunningPrompt()
    {
        var handler = new FakeComfyUiHandler(TimeSpan.FromMinutes(1), TimeProvider.System);
        var client = ClientFor(handler);
        var runner = new GenerationRunner(FastTiming, NullLogger<GenerationRunner>.Instance);
        string? promptId = null;

        var run = runner.RunAsync(client, Workflow("Native"), id => { promptId = id; return Task.CompletedTask; }, CancellationToken.None);
        await StudioAppFactory.WaitUntilAsync(() => Task.FromResult(promptId is not null), "prompt queued");
        Assert.True(await client.IsRunningAsync(promptId!, CancellationToken.None));
        await client.InterruptAsync(CancellationToken.None);

        var error = await Assert.ThrowsAsync<ComfyException>(() => run);
        Assert.Contains("execution_interrupted", error.Message);
    }

    private static int ReadBigEndian(byte[] bytes, int offset) =>
        bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3];
}

