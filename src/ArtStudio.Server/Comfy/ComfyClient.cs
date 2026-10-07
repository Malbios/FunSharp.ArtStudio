using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArtStudio.Server.Comfy;

public class ComfyException(string message) : Exception(message);

public sealed record ComfyImageRef(string Filename, string Subfolder, string Type);

public enum ComfyRunState
{
    Pending,
    Completed,
    Failed,
}

public sealed record ComfyRunStatus(ComfyRunState State, IReadOnlyList<ComfyImageRef> Images, string? Error)
{
    public static readonly ComfyRunStatus Pending = new(ComfyRunState.Pending, [], null);
}

public sealed record QueuedPrompt(string PromptId, string? NodeErrors);

public sealed class ComfyClient(HttpClient http)
{
    public const string HttpClientName = "comfy";

    private static readonly string[] FailureMessageTypes = ["execution_error", "execution_interrupted"];

    public async Task<QueuedPrompt> QueuePromptAsync(JsonObject workflow, CancellationToken ct)
    {
        var payload = new JsonObject
        {
            ["prompt"] = workflow,
            ["client_id"] = Guid.NewGuid().ToString(),
        };
        var result = await SendForJsonAsync(HttpMethod.Post, "/prompt", payload, ct);

        var promptId = result?["prompt_id"]?.GetValue<string>();
        if (string.IsNullOrEmpty(promptId))
            throw new ComfyException($"Workflow rejected:\n{result?.ToJsonString()}");

        var nodeErrors = result?["node_errors"] is JsonObject { Count: > 0 } errors ? errors.ToJsonString() : null;
        return new QueuedPrompt(promptId, nodeErrors);
    }

    public async Task<ComfyRunStatus> GetRunStatusAsync(string promptId, CancellationToken ct)
    {
        var history = await SendForJsonAsync(HttpMethod.Get, $"/history/{Uri.EscapeDataString(promptId)}", null, ct);
        return ParseRunStatus(history?[promptId]);
    }

    public async Task<byte[]> DownloadImageAsync(ComfyImageRef image, CancellationToken ct)
    {
        var query = $"filename={Uri.EscapeDataString(image.Filename)}" +
                    $"&subfolder={Uri.EscapeDataString(image.Subfolder)}" +
                    $"&type={Uri.EscapeDataString(image.Type)}";
        using var response = await http.GetAsync($"/view?{query}", ct);
        await EnsureSuccessAsync(response, "GET /view", ct);
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task InterruptAsync(CancellationToken ct)
    {
        await SendForJsonAsync(HttpMethod.Post, "/interrupt", new JsonObject(), ct);
    }

    public async Task<bool> IsRunningAsync(string promptId, CancellationToken ct)
    {
        var queue = await SendForJsonAsync(HttpMethod.Get, "/queue", null, ct);
        return (queue?["queue_running"] as JsonArray ?? [])
            .Any(entry => entry is JsonArray { Count: > 1 } item && item[1]?.GetValue<string>() == promptId);
    }

    public async Task DeleteFromQueueAsync(string promptId, CancellationToken ct)
    {
        await SendForJsonAsync(HttpMethod.Post, "/queue", new JsonObject { ["delete"] = new JsonArray(promptId) }, ct);
    }

    private static ComfyRunStatus ParseRunStatus(JsonNode? run)
    {
        if (run?["status"] is not JsonObject status)
            return ComfyRunStatus.Pending;

        if (status["status_str"]?.GetValue<string>() == "error")
            return Failed(status);

        var failureMessage = (status["messages"] as JsonArray ?? [])
            .FirstOrDefault(m => m is JsonArray { Count: > 0 } message &&
                                 FailureMessageTypes.Contains(message[0]?.GetValue<string>()));
        if (failureMessage is not null)
            return Failed(failureMessage);

        if (status["completed"]?.GetValue<bool>() != true)
            return ComfyRunStatus.Pending;

        return new ComfyRunStatus(ComfyRunState.Completed, ParseOutputImages(run["outputs"]), null);
    }

    private static ComfyRunStatus Failed(JsonNode details) =>
        new(ComfyRunState.Failed, [], $"Workflow execution failed:\n{details.ToJsonString()}");

    private static List<ComfyImageRef> ParseOutputImages(JsonNode? outputs)
    {
        if (outputs is not JsonObject outputNodes)
            return [];

        return outputNodes
            .SelectMany(output => output.Value?["images"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(image => new ComfyImageRef(
                image["filename"]?.GetValue<string>() ?? "",
                image["subfolder"]?.GetValue<string>() ?? "",
                image["type"]?.GetValue<string>() ?? ""))
            .ToList();
    }

    private async Task<JsonNode?> SendForJsonAsync(HttpMethod method, string path, JsonNode? payload, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (payload is not null)
            request.Content = JsonContent.Create(payload);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ComfyException($"ComfyUI request failed: {method} {path}\n{ex.Message}");
        }

        using (response)
        {
            await EnsureSuccessAsync(response, $"{method} {path}", ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body))
                return null;
            try
            {
                return JsonNode.Parse(body);
            }
            catch (JsonException)
            {
                throw new ComfyException($"ComfyUI returned non-JSON for {method} {path}:\n{Truncate(body)}");
            }
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string request, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;
        var body = await response.Content.ReadAsStringAsync(ct);
        throw new ComfyException($"ComfyUI request failed: {request} ({(int)response.StatusCode})\n{Truncate(body)}");
    }

    private static string Truncate(string text) => text.Length <= 2000 ? text : text[..2000] + "...";
}
