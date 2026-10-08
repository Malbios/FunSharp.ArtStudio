using ArtStudio.Server.Data;
using ArtStudio.Server.Generation;
using Microsoft.AspNetCore.DataProtection;

namespace ArtStudio.Server.Vision;

public sealed class VisionApiKey(IServiceScopeFactory scopeFactory, IDataProtectionProvider dataProtection)
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("ArtStudio.Vision");

    public async Task<bool> HasKeyAsync(CancellationToken ct) => await GetAsync(ct) is not null;

    public async Task<string?> GetAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var settings = await LoadSettingsAsync(scope, ct);
        return settings.ProtectedVisionApiKey is { } protectedKey ? _protector.Unprotect(protectedKey) : null;
    }

    public async Task SaveAsync(string apiKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new UserFacingException("Enter the API key of the vision server.");
        await UpdateAsync(_protector.Protect(apiKey.Trim()), ct);
    }

    public Task ClearAsync(CancellationToken ct) => UpdateAsync(null, ct);

    private async Task UpdateAsync(string? protectedKey, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var settings = await LoadSettingsAsync(scope, ct);
        settings.ProtectedVisionApiKey = protectedKey;
        await scope.ServiceProvider.GetRequiredService<StudioDbContext>().SaveChangesAsync(ct);
    }

    private static Task<Domain.StudioSettings> LoadSettingsAsync(IServiceScope scope, CancellationToken ct) =>
        SettingsStore.LoadAsync(
            scope.ServiceProvider.GetRequiredService<StudioDbContext>(), scope.ServiceProvider.GetRequiredService<AppPaths>(), ct);
}
