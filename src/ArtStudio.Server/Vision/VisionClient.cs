using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArtStudio.Server.Vision;

public class VisionException(string message) : Exception(message);

public sealed record VisionAnswer(string Text, bool Truncated);

public sealed class VisionClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
    public const string HttpClientName = "vision";
    public const string ServerUrlKey = "ArtStudio:VisionServerUrl";
    public const string DefaultServerUrl = "http://127.0.0.1:18080";
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(15);

    private const string ChatCompletionsPath = "/v1/chat/completions";
    private const string Model = "vision";
    private const double Temperature = 0.2;
    private const int MaxTokens = 4096;

    private string ServerUrl => (configuration[ServerUrlKey] is { Length: > 0 } configured ? configured : DefaultServerUrl).TrimEnd('/');

    public Task<VisionAnswer> DescribeAsync(
        string apiKey, byte[] image, string contentType, string instruction, CancellationToken ct) =>
        AskAsync(apiKey, new JsonArray(TextPart(instruction), ImagePart(image, contentType)), ct);

    public Task<VisionAnswer> CompleteAsync(string apiKey, string instruction, CancellationToken ct) =>
        AskAsync(apiKey, new JsonArray(TextPart(instruction)), ct);

    private async Task<VisionAnswer> AskAsync(string apiKey, JsonArray content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ServerUrl + ChatCompletionsPath)
        {
            Content = new StringContent(BuildPayload(content).ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new("Bearer", apiKey);

        using var response = await SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new VisionException("The vision server rejected the API key. Check it in Settings.");
        if (!response.IsSuccessStatusCode)
            throw new VisionException($"The vision server answered {(int)response.StatusCode}: {Shorten(body)}");

        return ParseAnswer(body);
    }

    private static JsonObject TextPart(string text) => new() { ["type"] = "text", ["text"] = text };

    private static JsonObject ImagePart(byte[] image, string contentType) => new()
    {
        ["type"] = "image_url",
        ["image_url"] = new JsonObject { ["url"] = $"data:{contentType};base64,{Convert.ToBase64String(image)}" },
    };

    private static JsonObject BuildPayload(JsonArray content) => new()
    {
        ["model"] = Model,
        ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = content }),
        ["temperature"] = Temperature,
        ["max_tokens"] = MaxTokens,
        ["stream"] = false,
    };

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is null)
        {
            throw new VisionException($"Could not reach the vision server at {ServerUrl}. Is the SSH tunnel running?");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new VisionException($"The vision server did not answer within {Timeout.TotalMinutes:0} minutes.");
        }
    }

    private static VisionAnswer ParseAnswer(string body)
    {
        JsonNode? choice;
        try
        {
            choice = JsonNode.Parse(body)?["choices"]?[0];
        }
        catch (JsonException)
        {
            throw new VisionException($"The vision server returned something that is not JSON: {Shorten(body)}");
        }

        var text = choice?["message"]?["content"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrEmpty(text))
            throw new VisionException("The vision server returned an empty answer.");
        return new VisionAnswer(text, choice?["finish_reason"]?.GetValue<string>() == "length");
    }

    private static string Shorten(string text) => text.Length <= 300 ? text : text[..300] + "…";
}
