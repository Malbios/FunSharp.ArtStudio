using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArtStudio.Server.Api;
using ArtStudio.Server.Comfy;
using ArtStudio.Server.DeviantArt;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArtStudio.Server.Tests.Fakes;

public sealed class StudioAppFactory : WebApplicationFactory<Program>
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public string DataDirectory { get; } =
        Path.Combine(Path.GetTempPath(), "ArtStudioTests", Guid.NewGuid().ToString("N"));

    public FakeComfyServer Comfy { get; } = new();

    public FakeDeviantArt DeviantArt { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ArtStudio:DataDirectory", DataDirectory);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<GenerationTiming>();
            services.AddSingleton(new GenerationTiming(TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(30)));
            services.AddHttpClient(ComfyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Comfy);
            services.AddHttpClient(DeviantArtService.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => DeviantArt);
        });
    }

    public async Task<int> CreateSetAsync(HttpClient client, string prompt, int count = 2, string resolution = "Native",
        Action<MultipartFormDataContent>? addSource = null)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(prompt), "prompt" },
            { new StringContent(resolution), "resolution" },
            { new StringContent(count.ToString()), "count" },
        };
        addSource?.Invoke(form);
        var response = await client.PostAsync("/api/sets", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    public static async Task<SetDetailDto> GetSetAsync(HttpClient client, int setId) =>
        (await client.GetFromJsonAsync<SetDetailDto>($"/api/sets/{setId}", Json))!;

    public static async Task<QueueDto> GetQueueAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<QueueDto>("/api/queue", Json))!;

    public static async Task WaitUntilAsync(Func<Task<bool>> condition, string description)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return;
            await Task.Delay(20);
        }
        throw new TimeoutException($"Timed out waiting for: {description}");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(DataDirectory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
