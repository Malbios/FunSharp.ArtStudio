using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArtStudio.Server.Data;
using ArtStudio.Server.Domain;
using ArtStudio.Server.Generation;
using Microsoft.AspNetCore.DataProtection;

namespace ArtStudio.Server.DeviantArt;

public sealed record DeviantArtConnectionStatus(string? ClientId, bool HasClientSecret, bool Connected, string? Username);

public sealed class DeviantArtAuth(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IDataProtectionProvider dataProtection,
    TimeProvider clock)
{
    public const string AuthorizeEndpoint = "https://www.deviantart.com/oauth2/authorize";
    public const string TokenEndpoint = "https://www.deviantart.com/oauth2/token";
    public const string WhoAmIEndpoint = "https://www.deviantart.com/api/v1/oauth2/user/whoami";
    private const string Scopes = "browse user";

    private static readonly TimeSpan PendingLoginLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AccessTokenSafetyMargin = TimeSpan.FromMinutes(1);

    private readonly IDataProtector _protector = dataProtection.CreateProtector("ArtStudio.DeviantArt");
    private readonly ConcurrentDictionary<string, PendingLogin> _pendingLogins = new();
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private CachedAccessToken? _accessToken;

    private sealed record PendingLogin(string CodeVerifier, string RedirectUri, DateTimeOffset ExpiresAt);

    private sealed record CachedAccessToken(string Token, DateTimeOffset ExpiresAt);

    private sealed record TokenResponse(string AccessToken, string RefreshToken, TimeSpan ExpiresIn);

    public async Task<DeviantArtConnectionStatus> GetStatusAsync(CancellationToken ct)
    {
        var settings = await LoadSettingsAsync(ct);
        return new DeviantArtConnectionStatus(
            settings.DeviantArtClientId,
            settings.ProtectedDeviantArtClientSecret is not null,
            settings.ProtectedDeviantArtRefreshToken is not null,
            settings.DeviantArtUsername);
    }

    public async Task<bool> IsConnectedAsync(CancellationToken ct) => (await GetStatusAsync(ct)).Connected;

    public async Task SaveAppCredentialsAsync(string clientId, string? clientSecret, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new UserFacingException("Enter the Client ID of your DeviantArt app.");

        await UpdateSettingsAsync(settings =>
        {
            var clientChanged = settings.DeviantArtClientId != clientId.Trim();
            settings.DeviantArtClientId = clientId.Trim();
            if (!string.IsNullOrWhiteSpace(clientSecret))
                settings.ProtectedDeviantArtClientSecret = _protector.Protect(clientSecret.Trim());
            if (clientChanged)
                ClearConnection(settings);
        }, ct);
    }

    public async Task<string> BuildAuthorizeUrlAsync(string redirectUri, CancellationToken ct)
    {
        var settings = await LoadSettingsAsync(ct);
        if (settings.DeviantArtClientId is null || settings.ProtectedDeviantArtClientSecret is null)
            throw new UserFacingException("Save the Client ID and Client Secret of your DeviantArt app first.");

        RemoveExpiredLogins();
        var state = RandomUrlSafeString();
        var codeVerifier = RandomUrlSafeString();
        _pendingLogins[state] = new PendingLogin(codeVerifier, redirectUri, clock.GetUtcNow() + PendingLoginLifetime);

        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = settings.DeviantArtClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = Scopes,
            ["state"] = state,
            ["code_challenge"] = CodeChallenge(codeVerifier),
            ["code_challenge_method"] = "S256",
        };
        return $"{AuthorizeEndpoint}?{string.Join('&', query.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"))}";
    }

    public async Task CompleteLoginAsync(string code, string state, CancellationToken ct)
    {
        if (!_pendingLogins.TryRemove(state, out var pending) || pending.ExpiresAt < clock.GetUtcNow())
            throw new UserFacingException("The DeviantArt login expired or was not started here. Please try again.");

        var settings = await LoadSettingsAsync(ct);
        var tokens = await RequestTokensAsync(settings, new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = pending.RedirectUri,
            ["code_verifier"] = pending.CodeVerifier,
        }, ct);

        var username = await FetchUsernameAsync(tokens.AccessToken, ct);
        await StoreTokensAsync(tokens, username, ct);
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
            if (settings.ProtectedDeviantArtRefreshToken is null)
                throw new UserFacingException("DeviantArt is not connected. Connect it in Settings.");

            TokenResponse tokens;
            try
            {
                tokens = await RequestTokensAsync(settings, new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = _protector.Unprotect(settings.ProtectedDeviantArtRefreshToken),
                }, ct);
            }
            catch (UserFacingException)
            {
                await DisconnectAsync(ct);
                throw new UserFacingException("The DeviantArt login expired. Reconnect it in Settings.");
            }

            await StoreTokensAsync(tokens, settings.DeviantArtUsername, ct);
            return tokens.AccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<TokenResponse> RequestTokensAsync(
        StudioSettings settings, Dictionary<string, string> grant, CancellationToken ct)
    {
        if (settings.DeviantArtClientId is null || settings.ProtectedDeviantArtClientSecret is null)
            throw new UserFacingException("The DeviantArt app credentials are missing. Add them in Settings.");

        grant["client_id"] = settings.DeviantArtClientId;
        grant["client_secret"] = _protector.Unprotect(settings.ProtectedDeviantArtClientSecret);

        using var response = await Http().PostAsync(TokenEndpoint, new FormUrlEncodedContent(grant), ct);
        var body = ParseJsonOrNull(await response.Content.ReadAsStringAsync(ct));
        var accessToken = body?["access_token"]?.GetValue<string>();
        var refreshToken = body?["refresh_token"]?.GetValue<string>();
        if (!response.IsSuccessStatusCode || accessToken is null || refreshToken is null)
        {
            var reason = body?["error_description"]?.GetValue<string>() ?? $"status {(int)response.StatusCode}";
            throw new UserFacingException($"DeviantArt refused the login: {reason}");
        }

        var expiresIn = TimeSpan.FromSeconds(body?["expires_in"]?.GetValue<int>() ?? 3600);
        return new TokenResponse(accessToken, refreshToken, expiresIn);
    }

    private async Task<string?> FetchUsernameAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, WhoAmIEndpoint);
        request.Headers.Authorization = new("Bearer", accessToken);
        using var response = await Http().SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;
        return ParseJsonOrNull(await response.Content.ReadAsStringAsync(ct))?["username"]?.GetValue<string>();
    }

    private async Task StoreTokensAsync(TokenResponse tokens, string? username, CancellationToken ct)
    {
        _accessToken = new CachedAccessToken(tokens.AccessToken, clock.GetUtcNow() + tokens.ExpiresIn - AccessTokenSafetyMargin);
        await UpdateSettingsAsync(settings =>
        {
            settings.ProtectedDeviantArtRefreshToken = _protector.Protect(tokens.RefreshToken);
            settings.DeviantArtUsername = username;
        }, ct);
    }

    private void ClearConnection(StudioSettings settings)
    {
        _accessToken = null;
        settings.ProtectedDeviantArtRefreshToken = null;
        settings.DeviantArtUsername = null;
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

    private static JsonNode? ParseJsonOrNull(string text)
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

    private HttpClient Http() => httpClientFactory.CreateClient(DeviantArtService.HttpClientName);
}
