using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Generation;
using Microsoft.AspNetCore.DataProtection;

namespace ArtStudio.Server.Google;

public sealed record GoogleConnectionStatus(
    string? ClientId, bool HasClientSecret, bool Connected, string? Account, DateTimeOffset? ConnectedAt, string OutputFolder);

public sealed class GoogleAuth(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IDataProtectionProvider dataProtection,
    IConfiguration configuration,
    TimeProvider clock)
{
    public const string HttpClientName = "google";
    public const string AuthorizeEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    public const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    public const string FakeAuthorizePath = "/api/google/fake-authorize";
    public const string FakeConfigurationKey = "ArtStudio:FakeGoogleDrive";
    // Full Drive access: clearing ComfyUI's output folder needs to trash files that Colab created.
    private const string Scope = "https://www.googleapis.com/auth/drive";

    private static readonly TimeSpan PendingLoginLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AccessTokenSafetyMargin = TimeSpan.FromMinutes(1);

    private readonly IDataProtector _protector = dataProtection.CreateProtector("ArtStudio.Google");
    private readonly ConcurrentDictionary<string, PendingLogin> _pendingLogins = new();
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private CachedAccessToken? _accessToken;

    private sealed record PendingLogin(string CodeVerifier, string RedirectUri, DateTimeOffset ExpiresAt);

    private sealed record CachedAccessToken(string Token, DateTimeOffset ExpiresAt);

    private sealed record TokenResponse(string AccessToken, string? RefreshToken, TimeSpan ExpiresIn);

    private bool UsesFakeGoogle => configuration.GetValue<bool>(FakeConfigurationKey);

    public async Task<GoogleConnectionStatus> GetStatusAsync(CancellationToken ct)
    {
        var settings = await LoadSettingsAsync(ct);
        return new GoogleConnectionStatus(
            settings.GoogleClientId,
            settings.ProtectedGoogleClientSecret is not null,
            settings.ProtectedGoogleRefreshToken is not null,
            settings.GoogleAccount,
            settings.GoogleConnectedAt,
            settings.GoogleDriveOutputFolder);
    }

    public async Task<bool> IsConnectedAsync(CancellationToken ct) => (await GetStatusAsync(ct)).Connected;

    public async Task SaveAppAsync(string clientId, string? clientSecret, string outputFolder, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new UserFacingException("Enter the Client ID of your Google OAuth client.");
        var folder = NormalizedFolder(outputFolder);

        await UpdateSettingsAsync(settings =>
        {
            var clientChanged = settings.GoogleClientId != clientId.Trim();
            settings.GoogleClientId = clientId.Trim();
            if (!string.IsNullOrWhiteSpace(clientSecret))
                settings.ProtectedGoogleClientSecret = _protector.Protect(clientSecret.Trim());
            settings.GoogleDriveOutputFolder = folder;
            if (clientChanged)
                ClearConnection(settings);
        }, ct);
    }

    private static string NormalizedFolder(string folder)
    {
        var parts = folder.Split('/', '\\').Select(part => part.Trim()).Where(part => part.Length > 0).ToList();
        return parts.Count > 0
            ? string.Join('/', parts)
            : throw new UserFacingException("Enter the Drive folder of ComfyUI's output, e.g. ComfyUI/output.");
    }

    public async Task<string> BuildAuthorizeUrlAsync(string redirectUri, CancellationToken ct)
    {
        var settings = await LoadSettingsAsync(ct);
        if (settings.GoogleClientId is null || settings.ProtectedGoogleClientSecret is null)
            throw new UserFacingException("Save the Client ID and Client Secret of your Google OAuth client first.");

        RemoveExpiredLogins();
        var state = RandomUrlSafeString();
        var codeVerifier = RandomUrlSafeString();
        _pendingLogins[state] = new PendingLogin(codeVerifier, redirectUri, clock.GetUtcNow() + PendingLoginLifetime);

        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = settings.GoogleClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = Scope,
            ["state"] = state,
            ["code_challenge"] = CodeChallenge(codeVerifier),
            ["code_challenge_method"] = "S256",
            // Without both, Google only hands out a refresh token on the very first consent.
            ["access_type"] = "offline",
            ["prompt"] = "consent",
        };
        var endpoint = UsesFakeGoogle ? FakeAuthorizePath : AuthorizeEndpoint;
        return $"{endpoint}?{string.Join('&', query.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"))}";
    }

    public async Task CompleteLoginAsync(string code, string state, CancellationToken ct)
    {
        if (!_pendingLogins.TryRemove(state, out var pending) || pending.ExpiresAt < clock.GetUtcNow())
            throw new UserFacingException("The Google login expired or was not started here. Please try again.");

        var settings = await LoadSettingsAsync(ct);
        var tokens = await RequestTokensAsync(settings, new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = pending.RedirectUri,
            ["code_verifier"] = pending.CodeVerifier,
        }, ct);
        var refreshToken = tokens.RefreshToken
            ?? throw new UserFacingException("Google did not grant offline access. Please try connecting again.");

        var account = await FetchAccountAsync(tokens.AccessToken, ct);
        _accessToken = CacheFor(tokens);
        await UpdateSettingsAsync(s =>
        {
            s.ProtectedGoogleRefreshToken = _protector.Protect(refreshToken);
            s.GoogleAccount = account;
            s.GoogleConnectedAt = clock.GetUtcNow();
        }, ct);
    }

    public async Task DisconnectAsync(CancellationToken ct)
    {
        await UpdateSettingsAsync(ClearConnection, ct);
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_accessToken is { } cached && cached.ExpiresAt > clock.GetUtcNow())
                return cached.Token;

            var settings = await LoadSettingsAsync(ct);
            if (settings.ProtectedGoogleRefreshToken is null)
                throw new UserFacingException("Google Drive is not connected. Connect it in Settings.");

            TokenResponse tokens;
            try
            {
                tokens = await RequestTokensAsync(settings, new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = _protector.Unprotect(settings.ProtectedGoogleRefreshToken),
                }, ct);
            }
            catch (LoginRevokedException)
            {
                await DisconnectAsync(ct);
                throw new UserFacingException(
                    "The Google Drive login expired (Google ends logins of apps in Testing after 7 days). Reconnect it in Settings.");
            }

            _accessToken = CacheFor(tokens);
            if (tokens.RefreshToken is { } rotated)
                await UpdateSettingsAsync(s => s.ProtectedGoogleRefreshToken = _protector.Protect(rotated), ct);
            return tokens.AccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>Forgets the cached access token, e.g. after Google answered 401 with it.</summary>
    public void ForgetAccessToken() => _accessToken = null;

    private sealed class LoginRevokedException() : Exception("The refresh token was rejected.");

    private async Task<TokenResponse> RequestTokensAsync(
        StudioSettings settings, Dictionary<string, string> grant, CancellationToken ct)
    {
        if (settings.GoogleClientId is null || settings.ProtectedGoogleClientSecret is null)
            throw new UserFacingException("The Google OAuth client is missing. Add it in Settings.");

        grant["client_id"] = settings.GoogleClientId;
        grant["client_secret"] = _protector.Unprotect(settings.ProtectedGoogleClientSecret);

        using var response = await Http().PostAsync(TokenEndpoint, new FormUrlEncodedContent(grant), ct);
        var body = ParseJsonOrNull(await response.Content.ReadAsStringAsync(ct));
        var accessToken = body?["access_token"]?.GetValue<string>();
        if (!response.IsSuccessStatusCode || accessToken is null)
        {
            // invalid_grant is Google's answer for an expired or revoked login; anything else may be temporary.
            if (body?["error"]?.GetValue<string>() == "invalid_grant" && grant["grant_type"] == "refresh_token")
                throw new LoginRevokedException();
            var reason = body?["error_description"]?.GetValue<string>()
                ?? body?["error"]?.GetValue<string>()
                ?? $"status {(int)response.StatusCode}";
            throw new UserFacingException($"Google refused the login: {reason}");
        }

        var expiresIn = TimeSpan.FromSeconds(body?["expires_in"]?.GetValue<int>() ?? 3600);
        return new TokenResponse(accessToken, body?["refresh_token"]?.GetValue<string>(), expiresIn);
    }

    private async Task<string?> FetchAccountAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{GoogleDrive.ApiBase}/about?fields=user(emailAddress)");
        request.Headers.Authorization = new("Bearer", accessToken);
        using var response = await Http().SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;
        return ParseJsonOrNull(await response.Content.ReadAsStringAsync(ct))?["user"]?["emailAddress"]?.GetValue<string>();
    }

    private CachedAccessToken CacheFor(TokenResponse tokens) =>
        new(tokens.AccessToken, clock.GetUtcNow() + tokens.ExpiresIn - AccessTokenSafetyMargin);

    private void ClearConnection(StudioSettings settings)
    {
        _accessToken = null;
        settings.ProtectedGoogleRefreshToken = null;
        settings.GoogleAccount = null;
        settings.GoogleConnectedAt = null;
    }

    private async Task<StudioSettings> LoadSettingsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        return await SettingsStore.LoadAsync(
            scope.ServiceProvider.GetRequiredService<StudioDbContext>(), scope.ServiceProvider.GetRequiredService<AppPaths>(), ct);
    }

    private async Task UpdateSettingsAsync(Action<StudioSettings> change, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
        var settings = await SettingsStore.LoadAsync(db, scope.ServiceProvider.GetRequiredService<AppPaths>(), ct);
        change(settings);
        await db.SaveChangesAsync(ct);
    }

    private void RemoveExpiredLogins()
    {
        var now = clock.GetUtcNow();
        foreach (var (state, login) in _pendingLogins)
        {
            if (login.ExpiresAt < now)
                _pendingLogins.TryRemove(state, out _);
        }
    }

    internal static JsonNode? ParseJsonOrNull(string text)
    {
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string RandomUrlSafeString() => Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string CodeChallenge(string codeVerifier) =>
        Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private HttpClient Http() => httpClientFactory.CreateClient(HttpClientName);
}
