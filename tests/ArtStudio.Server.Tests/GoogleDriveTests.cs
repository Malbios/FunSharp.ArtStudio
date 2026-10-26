using System.Net;
using System.Net.Http.Json;
using System.Web;
using ArtStudio.Server.Api;
using ArtStudio.Server.Generation;
using ArtStudio.Server.Google;
using ArtStudio.Server.Tests.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using static ArtStudio.Server.Tests.Fakes.StudioAppFactory;

namespace ArtStudio.Server.Tests;

public sealed class GoogleDriveTests : IDisposable
{
    private const string ClientSecret = "google-secret-value";

    private readonly StudioAppFactory _factory = new();
    private readonly HttpClient _client;

    public GoogleDriveTests() =>
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public void Dispose() => _factory.Dispose();

    private async Task SaveAppAsync(string outputFolder = "ComfyUI/output") =>
        (await _client.PutAsJsonAsync("/api/google/app", new { clientId = "google-client", clientSecret = ClientSecret, outputFolder }))
            .EnsureSuccessStatusCode();

    private async Task<Uri> StartLoginAsync()
    {
        var response = await _client.GetAsync("/api/google/login");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return response.Headers.Location!;
    }

    private async Task ConnectAsync()
    {
        await SaveAppAsync();
        var state = HttpUtility.ParseQueryString((await StartLoginAsync()).Query)["state"];
        var callback = await _client.GetAsync($"/api/google/callback?code=the-code&state={state}");
        Assert.Contains("google=connected", callback.Headers.Location!.ToString());
    }

    private Task<GoogleAppDto?> GetAppAsync() => _client.GetFromJsonAsync<GoogleAppDto>("/api/google/app", Json);

    private GoogleDrive Drive => _factory.Services.GetRequiredService<GoogleDrive>();

    [Fact]
    public async Task SavedSecret_IsNeverReturned()
    {
        await SaveAppAsync(" ComfyUI\\output/ ");

        var body = await _client.GetStringAsync("/api/google/app");

        Assert.DoesNotContain(ClientSecret, body);
        var app = await GetAppAsync();
        Assert.True(app!.HasClientSecret);
        Assert.Equal("google-client", app.ClientId);
        Assert.Equal("ComfyUI/output", app.OutputFolder);
        Assert.EndsWith("/api/google/callback", app.RedirectUri);
    }

    [Fact]
    public async Task Login_RedirectsToGoogleWithPkceAndOfflineAccess()
    {
        await SaveAppAsync();

        var authorize = await StartLoginAsync();

        var query = HttpUtility.ParseQueryString(authorize.Query);
        Assert.StartsWith(GoogleAuth.AuthorizeEndpoint, authorize.ToString());
        Assert.Equal("google-client", query["client_id"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal("offline", query["access_type"]);
        Assert.Equal("consent", query["prompt"]);
        Assert.Equal("https://www.googleapis.com/auth/drive", query["scope"]);
        Assert.False(string.IsNullOrEmpty(query["state"]));
    }

    [Fact]
    public async Task Login_WithoutClient_ReturnsToSettingsWithError()
    {
        var response = await _client.GetAsync("/api/google/login");

        Assert.Contains("/settings?google=error", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Callback_ExchangesCodeAndStoresAccount()
    {
        await ConnectAsync();

        var app = await GetAppAsync();
        Assert.True(app!.Connected);
        Assert.Equal(FakeGoogle.Account, app.Account);
        Assert.NotNull(app.ConnectedAt);
        var exchange = _factory.Google.TokenRequests.Single();
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

        var callback = await _client.GetAsync("/api/google/callback?code=the-code&state=forged");

        Assert.Contains("google=error", callback.Headers.Location!.ToString());
        Assert.False((await GetAppAsync())!.Connected);
        Assert.Empty(_factory.Google.TokenRequests);
    }

    [Fact]
    public async Task ExpiredAccessToken_IsRefreshedWithTheSameRefreshToken()
    {
        _factory.Google.AccessTokenLifetimeSeconds = 0;
        await ConnectAsync();
        var auth = _factory.Services.GetRequiredService<GoogleAuth>();

        var first = await auth.GetAccessTokenAsync(CancellationToken.None);
        var second = await auth.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(["access-2", "access-3"], [first, second]);
        var refreshes = _factory.Google.TokenRequests.Where(r => r["grant_type"] == "refresh_token").ToList();
        Assert.Equal(["refresh-1", "refresh-1"], refreshes.Select(r => r["refresh_token"]));
    }

    [Fact]
    public async Task RejectedRefresh_DisconnectsWithSevenDayHint()
    {
        _factory.Google.AccessTokenLifetimeSeconds = 0;
        await ConnectAsync();
        _factory.Google.RejectRefresh = true;
        var auth = _factory.Services.GetRequiredService<GoogleAuth>();

        var error = await Assert.ThrowsAsync<UserFacingException>(() => auth.GetAccessTokenAsync(CancellationToken.None));

        Assert.Contains("7 days", error.Message);
        Assert.Contains("Reconnect", error.Message);
        Assert.False((await GetAppAsync())!.Connected);
    }

    [Fact]
    public async Task Disconnect_KeepsTheClient()
    {
        await ConnectAsync();

        (await _client.PostAsync("/api/google/disconnect", null)).EnsureSuccessStatusCode();

        var app = await GetAppAsync();
        Assert.False(app!.Connected);
        Assert.Null(app.Account);
        Assert.True(app.HasClientSecret);
    }

    [Fact]
    public async Task Drive_FindsFolderByPathAndDownloadsCompleteFile()
    {
        await ConnectAsync();
        var file = _factory.Google.AddFile("ComfyUI/output/video", "clip_00001_.mp4", [1, 2, 3]);
        _factory.Google.AddFile("ComfyUI/output/video", "other_00001_.mp4", [9]);

        var folderId = await Drive.FindFolderAsync("ComfyUI/output/video", CancellationToken.None);
        var found = await Drive.FindCompleteFileAsync(folderId, "clip_00001_.mp4", CancellationToken.None);
        using var content = new MemoryStream();
        await Drive.DownloadAsync(found!, content, CancellationToken.None);

        Assert.Equal(file.Id, found!.Id);
        Assert.Equal(3, found.Size);
        Assert.Equal([1, 2, 3], content.ToArray());
    }

    [Fact]
    public async Task Drive_MissingFolderOrFile_IsReported()
    {
        await ConnectAsync();
        var folderId = _factory.Google.AddFolder("ComfyUI/output/video");

        var missingFolder = await Assert.ThrowsAsync<UserFacingException>(() =>
            Drive.FindFolderAsync("ComfyUI/elsewhere", CancellationToken.None));
        var missingFile = await Drive.FindCompleteFileAsync(folderId, "nothing.mp4", CancellationToken.None);

        Assert.Contains("'ComfyUI/elsewhere'", missingFolder.Message);
        Assert.Null(missingFile);
    }

    [Fact]
    public async Task Drive_WithoutConnection_AsksToConnect()
    {
        var error = await Assert.ThrowsAsync<UserFacingException>(() =>
            Drive.FindFolderAsync("ComfyUI/output", CancellationToken.None));

        Assert.Contains("Connect it in Settings", error.Message);
    }
}
