using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ArtStudio.Server.Tests.Fakes.StudioAppFactory;

namespace ArtStudio.Server.Tests;

public sealed class RestartTests
{
    private static async Task<GenerationJob> SingleJobAsync(StudioAppFactory app)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<StudioDbContext>().Jobs.AsNoTracking().SingleAsync();
    }

    private static Task WaitForJobAsync(HttpClient client, int setId, JobStatus status) =>
        WaitUntilAsync(async () => (await GetSetAsync(client, setId)).Jobs.Single().Status == status, $"job {status}");

    [Fact]
    public async Task Restart_CollectsTheImageComfyWasRendering()
    {
        var comfy = new FakeComfyServer { HoldRuns = true };
        var dataDirectory = Path.Combine(Path.GetTempPath(), "ArtStudioTests", Guid.NewGuid().ToString("N"));

        int setId;
        using (var before = new StudioAppFactory { DataDirectory = dataDirectory, Comfy = comfy, KeepDataDirectory = true })
        {
            var client = before.CreateClient();
            setId = await before.CreateSetAsync(client, "fox", count: 1);
            await WaitUntilAsync(async () => (await SingleJobAsync(before)).CurrentComfyPromptId is not null, "run id stored");
        }

        comfy.HoldRuns = false;
        using var after = new StudioAppFactory { DataDirectory = dataDirectory, Comfy = comfy };
        var restartedClient = after.CreateClient();
        await WaitForJobAsync(restartedClient, setId, JobStatus.Completed);

        var image = Assert.Single((await GetSetAsync(restartedClient, setId)).Images);
        Assert.Single(comfy.SubmittedPrompts);
        Assert.Equal(comfy.SubmittedSeeds.Single().ToString(), image.Seed);
        var job = await SingleJobAsync(after);
        Assert.Null(job.CurrentComfyPromptId);
        Assert.Null(job.CurrentSeed);
    }

    [Fact]
    public async Task RunUnknownToComfy_IsRenderedAgain()
    {
        using var app = new StudioAppFactory();
        var client = app.CreateClient();
        (await client.PostAsync("/api/queue/pause", null)).EnsureSuccessStatusCode();
        var setId = await app.CreateSetAsync(client, "fox", count: 1);
        using (var scope = app.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<StudioDbContext>().Jobs.ExecuteUpdateAsync(s => s
                .SetProperty(j => j.CurrentComfyPromptId, "lost-run")
                .SetProperty(j => j.CurrentSeed, 5));
        }

        (await client.PostAsync("/api/queue/resume", null)).EnsureSuccessStatusCode();
        await WaitForJobAsync(client, setId, JobStatus.Completed);

        var image = Assert.Single((await GetSetAsync(client, setId)).Images);
        Assert.Single(app.Comfy.SubmittedPrompts);
        Assert.Equal(app.Comfy.SubmittedSeeds.Single().ToString(), image.Seed);
    }
}
