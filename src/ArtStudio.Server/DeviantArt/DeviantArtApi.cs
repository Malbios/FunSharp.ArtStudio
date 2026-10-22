using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArtStudio.Server.Generation;

namespace ArtStudio.Server.DeviantArt;

public sealed class DeviantArtRateLimitedException(string message) : UserFacingException(message);

/// <summary>Calls DeviantArt's API as the connected account and turns its refusals into messages for the user.</summary>
public sealed class DeviantArtApi(IHttpClientFactory httpClientFactory, DeviantArtAuth auth)
{
    public const string ApiBase = "https://www.deviantart.com/api/v1/oauth2";

    public async Task<JsonNode> SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, $"{ApiBase}/{path}") { Content = content };
        request.Headers.Authorization = new("Bearer", await auth.GetAccessTokenAsync(ct));
        using var response = await httpClientFactory.CreateClient(DeviantArtService.HttpClientName).SendAsync(request, ct);
        var body = ParseJsonOrNull(await response.Content.ReadAsStringAsync(ct));

        if (response.IsSuccessStatusCode && body is not null)
            return body;
        var message = ErrorMessage(response.StatusCode, body);
        throw response.StatusCode == HttpStatusCode.TooManyRequests
            ? new DeviantArtRateLimitedException(message)
            : new UserFacingException(message);
    }

    private static string ErrorMessage(HttpStatusCode status, JsonNode? body)
    {
        var error = body?["error"]?.GetValue<string>();
        var description = body?["error_description"]?.GetValue<string>();
        return status switch
        {
            HttpStatusCode.Unauthorized => "DeviantArt rejected the login. Reconnect it in Settings.",
            HttpStatusCode.Forbidden when error == "insufficient_scope" || description?.Contains("scope") == true =>
                "The DeviantArt login does not allow Stash and publishing yet. Reconnect DeviantArt in Settings.",
            HttpStatusCode.TooManyRequests => "DeviantArt is limiting requests. Try again in a minute.",
            _ when description is not null => $"DeviantArt refused: {description}",
            _ when body is null => $"DeviantArt returned an unreadable response ({(int)status}).",
            _ => $"DeviantArt refused the request ({(int)status}).",
        };
    }

    private static JsonNode? ParseJsonOrNull(string text)
    {
        try
        {
            return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
