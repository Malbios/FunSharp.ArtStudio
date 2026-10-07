using System.Net;
using System.Net.Http.Json;
using System.Web;
using ArtStudio.Server.Api;
using ArtStudio.Server.DeviantArt;
using ArtStudio.Server.Generation;
using ArtStudio.Server.Tests.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using static ArtStudio.Server.Tests.Fakes.StudioAppFactory;

namespace ArtStudio.Server.Tests;

public sealed class DeviantArtLoginTests : IDisposable
{
    private const string ClientSecret = "super-secret-value";

    private readonly StudioAppFactory _factory = new();
    private readonly HttpClient _client;

    public DeviantArtLoginTests() =>
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public void Dispose() => _factory.Dispose();

    private async Task SaveAppAsync() =>
        (await _client.PutAsJsonAsync("/api/deviantart/app", new { clientId = "12345", clientSecret = ClientSecret }))
            .EnsureSuccessStatusCode();

    private async Task<Uri> StartLoginAsync()
    {
        var response = await _client.GetAsync("/api/deviantart/login");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return response.Headers.Location!;
    }

    private async Task ConnectAsync()
    {
        await SaveAppAsync();
        var state = HttpUtility.ParseQueryString((await StartLoginAsync()).Query)["state"];
        var callback = await _client.GetAsync($"/api/deviantart/callback?code=the-code&state={state}");
        Assert.Contains("deviantart=connected", callback.Headers.Location!.ToString());
    }

    private Task<DeviantArtAppDto?> GetAppAsync() =>
        _client.GetFromJsonAsync<DeviantArtAppDto>("/api/deviantart/app", Json);

    [Fact]
    public async Task SavedSecret_IsNeverReturned()
    {
        await SaveAppAsync();

        var body = await _client.GetStringAsync("/api/deviantart/app");

        Assert.DoesNotContain(ClientSecret, body);
        var app = await GetAppAsync();
        Assert.True(app!.HasClientSecret);
        Assert.Equal("12345", app.ClientId);
        Assert.EndsWith("/api/deviantart/callback", app.RedirectUri);
    }

    [Fact]
    public async Task Login_RedirectsToDeviantArtWithPkce()
    {
        await SaveAppAsync();

        var authorize = await StartLoginAsync();

        var query = HttpUtility.ParseQueryString(authorize.Query);
        Assert.StartsWith(DeviantArtAuth.AuthorizeEndpoint, authorize.ToString());
        Assert.Equal("12345", query["client_id"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrEmpty(query["code_challenge"]));
        Assert.False(string.IsNullOrEmpty(query["state"]));
    }

    [Fact]
    public async Task Login_WithoutAppCredentials_ReturnsToSettingsWithError()
    {
        var response = await _client.GetAsync("/api/deviantart/login");

        Assert.Contains("/settings?deviantart=error", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Callback_ExchangesCodeAndStoresConnection()
    {
        await ConnectAsync();

        var app = await GetAppAsync();
        Assert.True(app!.Connected);
        Assert.Equal(FakeDeviantArt.Username, app.Username);
        var exchange = _factory.DeviantArt.TokenRequests.Single();
        Assert.Equal("authorization_code", exchange["grant_type"]);
        Assert.Equal("the-code", exchange["code"]);
        Assert.Equal(ClientSecret, exchange["client_secret"]);
        Assert.False(string.IsNullOrEmpty(exchange["code_verifier"]));
    }

    [Fact]
    public async Task Callback_WithUnknownState_IsRejected()
    {
        await SaveAppAsync();
        await StartLoginAsync();

        var callback = await _client.GetAsync("/api/deviantart/callback?code=the-code&state=forged");

        Assert.Contains("deviantart=error", callback.Headers.Location!.ToString());
        Assert.False((await GetAppAsync())!.Connected);
        Assert.Empty(_factory.DeviantArt.TokenRequests);
    }

    [Fact]
    public async Task ExpiredAccessToken_IsRefreshedWithRotatedRefreshToken()
    {
        _factory.DeviantArt.AccessTokenLifetimeSeconds = 0;
        await ConnectAsync();
        var auth = _factory.Services.GetRequiredService<DeviantArtAuth>();

        var first = await auth.GetAccessTokenAsync(CancellationToken.None);
        var second = await auth.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("access-2", first);
        Assert.Equal("access-3", second);
        var refreshes = _factory.DeviantArt.TokenRequests.Where(r => r["grant_type"] == "refresh_token").ToList();
        Assert.Equal(["refresh-1", "refresh-2"], refreshes.Select(r => r["refresh_token"]));
    }

    [Fact]
    public async Task RejectedRefresh_DisconnectsWithHint()
    {
        _factory.DeviantArt.AccessTokenLifetimeSeconds = 0;
        await ConnectAsync();
        _factory.DeviantArt.RejectRefresh = true;
        var auth = _factory.Services.GetRequiredService<DeviantArtAuth>();

        var error = await Assert.ThrowsAsync<UserFacingException>(() => auth.GetAccessTokenAsync(CancellationToken.None));

        Assert.Contains("Reconnect", error.Message);
        Assert.False((await GetAppAsync())!.Connected);
    }

    [Fact]
    public async Task Disconnect_ClearsConnection()
    {
        await ConnectAsync();

        (await _client.PostAsync("/api/deviantart/disconnect", null)).EnsureSuccessStatusCode();

        var app = await GetAppAsync();
        Assert.False(app!.Connected);
        Assert.True(app.HasClientSecret);
    }
}
