using ArtStudio.Server.DeviantArt;
using ArtStudio.Server.Generation;

namespace ArtStudio.Server.Api;

public sealed record DeviantArtAppDto(
    string? ClientId, bool HasClientSecret, bool Connected, string? Username, string RedirectUri);

public sealed record DeviantArtAppRequest(string ClientId, string? ClientSecret);

public static class DeviantArtLoginEndpoints
{
    private const string CallbackPath = "/api/deviantart/callback";

    public static void MapDeviantArtLoginEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/deviantart/app", GetAppAsync);
        app.MapPut("/api/deviantart/app", async (DeviantArtAppRequest request, HttpRequest http, DeviantArtAuth auth, CancellationToken ct) =>
        {
            await auth.SaveAppCredentialsAsync(request.ClientId, request.ClientSecret, ct);
            return await GetAppAsync(http, auth, ct);
        });
        app.MapPost("/api/deviantart/disconnect", (DeviantArtAuth auth, CancellationToken ct) => auth.DisconnectAsync(ct));
        app.MapGet("/api/deviantart/login", LoginAsync);
        app.MapGet(CallbackPath, CallbackAsync);
    }

    private static async Task<DeviantArtAppDto> GetAppAsync(HttpRequest http, DeviantArtAuth auth, CancellationToken ct)
    {
        var status = await auth.GetStatusAsync(ct);
        return new DeviantArtAppDto(status.ClientId, status.HasClientSecret, status.Connected, status.Username, RedirectUri(http));
    }

    private static async Task<IResult> LoginAsync(HttpRequest http, DeviantArtAuth auth, CancellationToken ct)
    {
        try
        {
            return Results.Redirect(await auth.BuildAuthorizeUrlAsync(RedirectUri(http), ct));
        }
        catch (UserFacingException ex)
        {
            return BackToSettings("error", ex.Message);
        }
    }

    private static async Task<IResult> CallbackAsync(
        string? code, string? state, string? error, string? error_description, DeviantArtAuth auth, CancellationToken ct)
    {
        if (error is not null || code is null || state is null)
            return BackToSettings("error", error_description ?? error ?? "DeviantArt did not return a login code.");

        try
        {
            await auth.CompleteLoginAsync(code, state, ct);
            return BackToSettings("connected", null);
        }
        catch (UserFacingException ex)
        {
            return BackToSettings("error", ex.Message);
        }
    }

    private static IResult BackToSettings(string outcome, string? message)
    {
        var query = $"deviantart={outcome}" + (message is null ? "" : $"&message={Uri.EscapeDataString(message)}");
        return Results.Redirect($"/settings?{query}#deviantart");
    }

    private static string RedirectUri(HttpRequest http) => $"{http.Scheme}://{http.Host}{CallbackPath}";
}
