using System.Text.Json.Serialization;
using ArtStudio.Server.Comfy;
using ArtStudio.Server.Data;
using ArtStudio.Server.DeviantArt;
using ArtStudio.Server.Generation;
using ArtStudio.Server.Hubs;
using ArtStudio.Server.Vision;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace ArtStudio.Server;

public static class ServiceRegistration
{
    public static IServiceCollection AddArtStudio(this IServiceCollection services)
    {
        services.AddSingleton(sp => AppPaths.FromConfiguration(sp.GetRequiredService<IConfiguration>()));
        services.AddDbContext<StudioDbContext>((sp, options) =>
            options.UseSqlite($"Data Source={sp.GetRequiredService<AppPaths>().DatabasePath}"));

        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddSignalR().AddJsonProtocol(options =>
            options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(sp => new FakeComfyUiHandler(TimeSpan.FromSeconds(2), sp.GetRequiredService<TimeProvider>()));
        services.AddHttpClient(ComfyClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(sp =>
            sp.GetRequiredService<IConfiguration>().GetValue<bool>(FakeComfyUiHandler.ConfigurationKey)
                ? sp.GetRequiredService<FakeComfyUiHandler>()
                : new SocketsHttpHandler());
        services.AddSingleton<ComfyClientFactory>();
        services.AddSingleton(ComfyWorkflowBuilder.FromEmbeddedTemplate());
        services.AddSingleton<SeedGenerator>();
        services.AddSingleton(GenerationTiming.Default);
        services.AddSingleton<GenerationRunner>();
        services.AddSingleton<GenerationQueue>();
        services.AddSingleton<StudioNotifier>();
        services.AddScoped<QueueService>();
        services.AddScoped<SetService>();
        services.AddHttpClient(DeviantArtService.HttpClientName, http =>
        {
            http.Timeout = TimeSpan.FromSeconds(30);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("ArtStudio/1.0");
        });
        services.AddScoped<DeviantArtService>();
        services.AddDataProtection().SetApplicationName("ArtStudio");
        services.AddSingleton<DeviantArtAuth>();
        services.AddHostedService<QueueWorker>();

        services.AddHttpClient(VisionClient.HttpClientName, http => http.Timeout = VisionClient.Timeout)
            .ConfigurePrimaryHttpMessageHandler(sp =>
                sp.GetRequiredService<IConfiguration>().GetValue<bool>(FakeVisionHandler.ConfigurationKey)
                    ? new FakeVisionHandler(imageAnswerTime: TimeSpan.FromSeconds(5), textAnswerTime: TimeSpan.FromSeconds(2))
                    : new SocketsHttpHandler());
        services.AddSingleton<VisionClient>();
        services.AddSingleton<PromptModifier>();
        services.AddSingleton(VisionInstruction.FromEmbeddedText());
        services.AddSingleton<VisionApiKey>();
        services.AddSingleton<PromptGenerationQueue>();
        services.AddHostedService<PromptGenerationWorker>();
        return services;
    }
}
