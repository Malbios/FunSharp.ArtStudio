using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ArtStudio.Server.Comfy;

/// <summary>
/// Stands in for a ComfyUI server so the app can be exercised without a GPU: every prompt "renders" after a short
/// delay into a solid-color PNG at the requested resolution.
/// </summary>
public sealed partial class FakeComfyUiHandler(TimeSpan renderTime, TimeProvider clock) : HttpMessageHandler
{
    public const string ConfigurationKey = "ArtStudio:FakeComfyUi";

    private const string ResolutionNodeId = "80";
    private const string SeedNodeId = "79:129";

    private readonly ConcurrentDictionary<string, FakeRun> _runs = new();

    private sealed record FakeRun(DateTimeOffset ReadyAt, int Width, int Height, long Seed)
    {
        public bool Interrupted { get; set; }
    }

    [GeneratedRegex(@"^(?<width>\d+)×(?<height>\d+)")]
    private static partial Regex ResolutionLabel();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);

        return (request.Method.Method, path) switch
        {
            ("POST", "/prompt") => QueuePrompt(body!),
            ("GET", var p) when p.StartsWith("/history/") => History(p["/history/".Length..]),
            ("GET", "/view") => View(request.RequestUri),
            ("GET", "/queue") => Json(new JsonObject
            {
                ["queue_running"] = new JsonArray(RunningPromptIds().Select(id => (JsonNode)new JsonArray(0, id)).ToArray()),
                ["queue_pending"] = new JsonArray(),
            }),
            ("POST", "/queue") => Json(new JsonObject()),
            ("POST", "/interrupt") => Interrupt(),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
    }

    private HttpResponseMessage QueuePrompt(string body)
    {
        var workflow = JsonNode.Parse(body)!["prompt"]!;
        var label = workflow[ResolutionNodeId]?["inputs"]?["resolution"]?.GetValue<string>() ?? "";
        var match = ResolutionLabel().Match(label);
        var (width, height) = match.Success
            ? (int.Parse(match.Groups["width"].Value), int.Parse(match.Groups["height"].Value))
            : (1024, 1024);
        var seed = workflow[SeedNodeId]?["inputs"]?["seed"]?.GetValue<long>() ?? 0;

        var promptId = Guid.NewGuid().ToString();
        _runs[promptId] = new FakeRun(clock.GetUtcNow() + renderTime, width, height, seed);
        return Json(new JsonObject { ["prompt_id"] = promptId, ["number"] = _runs.Count, ["node_errors"] = new JsonObject() });
    }

    private HttpResponseMessage History(string promptId)
    {
        if (!_runs.TryGetValue(promptId, out var run))
            return Json(new JsonObject());

        if (run.Interrupted)
            return Json(new JsonObject
            {
                [promptId] = new JsonObject
                {
                    ["status"] = new JsonObject
                    {
                        ["status_str"] = "error",
                        ["completed"] = false,
                        ["messages"] = new JsonArray(new JsonArray("execution_interrupted", new JsonObject())),
                    },
                },
            });

        if (clock.GetUtcNow() < run.ReadyAt)
            return Json(new JsonObject());

        return Json(new JsonObject
        {
            [promptId] = new JsonObject
            {
                ["status"] = new JsonObject { ["status_str"] = "success", ["completed"] = true, ["messages"] = new JsonArray() },
                ["outputs"] = new JsonObject
                {
                    ["78"] = new JsonObject
                    {
                        ["images"] = new JsonArray(new JsonObject
                        {
                            ["filename"] = $"{promptId}.png",
                            ["subfolder"] = "",
                            ["type"] = "output",
                        }),
                    },
                },
            },
        });
    }

    private HttpResponseMessage View(Uri uri)
    {
        var fileName = System.Web.HttpUtility.ParseQueryString(uri.Query)["filename"] ?? "";
        if (!_runs.TryGetValue(Path.GetFileNameWithoutExtension(fileName), out var run))
            return new HttpResponseMessage(HttpStatusCode.NotFound);

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(SolidColorPng(run.Width, run.Height, ColorFor(run.Seed))),
        };
        response.Content.Headers.ContentType = new("image/png");
        return response;
    }

    private HttpResponseMessage Interrupt()
    {
        foreach (var promptId in RunningPromptIds())
            _runs[promptId].Interrupted = true;
        return Json(new JsonObject());
    }

    private IEnumerable<string> RunningPromptIds() =>
        _runs.Where(run => !run.Value.Interrupted && clock.GetUtcNow() < run.Value.ReadyAt).Select(run => run.Key).ToList();

    private static HttpResponseMessage Json(JsonNode body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };

    private static (byte Red, byte Green, byte Blue) ColorFor(long seed) =>
        ((byte)(80 + seed % 150), (byte)(80 + seed / 7 % 150), (byte)(80 + seed / 13 % 150));

    internal static byte[] SolidColorPng(int width, int height, (byte Red, byte Green, byte Blue) color)
    {
        var row = new byte[1 + width * 3];
        for (var x = 0; x < width; x++)
        {
            row[1 + x * 3] = color.Red;
            row[2 + x * 3] = color.Green;
            row[3 + x * 3] = color.Blue;
        }

        using var pixels = new MemoryStream();
        using (var zlib = new ZLibStream(pixels, CompressionLevel.Fastest, leaveOpen: true))
        {
            for (var y = 0; y < height; y++)
                zlib.Write(row);
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(png, "IHDR", [.. BigEndian(width), .. BigEndian(height), 8, 2, 0, 0, 0]);
        WriteChunk(png, "IDAT", pixels.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(BigEndian(data.Length));
        stream.Write(typeBytes);
        stream.Write(data);
        stream.Write(BigEndian((int)Crc32([.. typeBytes, .. data])));
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }

    private static byte[] BigEndian(int value) =>
        [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
}
