using ArtStudio.Server.Domain;

namespace ArtStudio.Server.Data;

public static class SettingsStore
{
    public const string DefaultComfyServerUrl = "https://wise-terminally-cockatoo.ngrok-free.app";

    public static async Task<StudioSettings> LoadAsync(StudioDbContext db, AppPaths paths, CancellationToken ct = default)
    {
        var settings = await db.Settings.FindAsync([StudioSettings.SingletonId], ct);
        if (settings is not null)
            return settings;

        settings = new StudioSettings
        {
            ComfyServerUrl = DefaultComfyServerUrl,
            OutputDirectory = paths.DefaultOutputDirectory,
        };
        db.Settings.Add(settings);
        await db.SaveChangesAsync(ct);
        return settings;
    }
}
