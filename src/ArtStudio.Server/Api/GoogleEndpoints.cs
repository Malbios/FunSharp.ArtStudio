using ArtStudio.Server.Generation;
using ArtStudio.Server.Google;

namespace ArtStudio.Server.Api;

public sealed record GoogleAppDto(
    string? ClientId,
    bool HasClientSecret,
    bool Connected,
    string? Account,
    DateTimeOffset? ConnectedAt,
    string OutputFolder,
    string RedirectUri);

public sealed record GoogleAppRequest(string ClientId, string? ClientSecret, string OutputFolder);

public static class GoogleEndpoints
{
    private const string CallbackPath = "/api/google/callback";

    public static void MapGoogleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/google/app", GetAppAsync);
        app.MapPut("/api/google/app", async (GoogleAppRequest request, HttpRequest http, GoogleAuth auth, CancellationToken ct) =>
        {
            await auth.SaveAppAsync(request.ClientId, request.ClientSecret, request.OutputFolder, ct);
            return await GetAppAsync(http, auth, ct);
        });
        app.MapPost("/api/google/disconnect", (GoogleAuth auth, CancellationToken ct) => auth.DisconnectAsync(ct));
        app.MapGet("/api/google/login", LoginAsync);
        app.MapGet(CallbackPath, CallbackAsync);
        app.MapGet("/api/google/output-folder", (ComfyOutputFolder folder, CancellationToken ct) => folder.MeasureAsync(ct));
        app.MapPost("/api/google/output-folder/clear", (ComfyOutputFolder folder, CancellationToken ct) => folder.ClearAsync(ct));

        if (app.ServiceProvider.GetRequiredService<IConfiguration>().GetValue<bool>(GoogleAuth.FakeConfigurationKey))
            app.MapGet(GoogleAuth.FakeAuthorizePath, (string redirect_uri, string state) =>
                Results.Redirect($"{redirect_uri}?code=fake-code&state={Uri.EscapeDataString(state)}"));
    }

    private static async Task<GoogleAppDto> GetAppAsync(HttpRequest http, GoogleAuth auth, CancellationToken ct)
    {
        var status = await auth.GetStatusAsync(ct);
        return new GoogleAppDto(
            status.ClientId, status.HasClientSecret, status.Connected, status.Account, status.ConnectedAt, status.OutputFolder,
            RedirectUri(http));
    }

    private static async Task<IResult> LoginAsync(HttpRequest http, GoogleAuth auth, CancellationToken ct)
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
        string? code, string? state, string? error, string? error_description, GoogleAuth auth, CancellationToken ct)
    {
        if (error is not null || code is null || state is null)
            return BackToSettings("error", error_description ?? error ?? "Google did not return a login code.");

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
        var query = $"google={outcome}" + (message is null ? "" : $"&message={Uri.EscapeDataString(message)}");
        return Results.Redirect($"/settings?{query}#google-drive");
    }

    private static string RedirectUri(HttpRequest http) => $"{http.Scheme}://{http.Host}{CallbackPath}";
}
