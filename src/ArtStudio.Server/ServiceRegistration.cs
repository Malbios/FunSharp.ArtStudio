using System.Text.Json.Serialization;
using ArtStudio.Server.Comfy;
using ArtStudio.Server.Data;
using ArtStudio.Server.DeviantArt;
using ArtStudio.Server.Generation;
using ArtStudio.Server.Hubs;
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
        return services;
    }
}
