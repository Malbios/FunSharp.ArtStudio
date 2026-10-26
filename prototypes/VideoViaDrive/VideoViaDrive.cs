#:package Google.Apis.Drive.v3@1.77.0.4276
#:property PublishAot=false

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;

const string TemplateFileName = "minimax_h3_i2v.json";
const string SaveVideoNodeId = "92";
const string FirstFrameNodeId = "114";
const string LastFrameNodeId = "128";
const string VideoNodeId = "135:105:104";
const string SecondsNodeId = "135:279";
const string SeedNodeId = "135:223:139";
const string LightningLoraNodeId = "135:105:218:126";
const string PromptPlaceholder = "???";
const string FolderMimeType = "application/vnd.google-apps.folder";

var pollInterval = TimeSpan.FromSeconds(5);
var renderTimeout = TimeSpan.FromMinutes(30);
var driveTimeout = TimeSpan.FromMinutes(15);
var settingsDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArtStudio-prototype");
var clientSecretPath = Path.Combine(settingsDirectory, "client_secret.json");
var prototypeDirectory = AppContext.GetData("EntryPointFileDirectoryPath") as string ?? Environment.CurrentDirectory;
var outputDirectory = Path.Combine(prototypeDirectory, "output");

if (args.Contains("--help") || args.Length == 0)
{
    PrintHelp();
    return 0;
}

Options options;
try
{
    options = Options.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine("Run with --help for the options.");
    return 1;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};
var ct = cancellation.Token;

var runName = $"artstudio-proto-{DateTime.Now:yyyyMMdd-HHmmss}";
Directory.CreateDirectory(outputDirectory);

if (options.DryRun)
{
    var dryRunWorkflow = BuildWorkflow(
        Path.GetFileName(options.StartImage), options.EndImage is null ? null : Path.GetFileName(options.EndImage));
    var workflowPath = Path.Combine(outputDirectory, $"{runName}-workflow.json");
    await File.WriteAllTextAsync(workflowPath, dryRunWorkflow.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ct);
    Console.WriteLine($"Dry run: filled workflow written to {workflowPath}. Nothing was sent.");
    return 0;
}

if (!File.Exists(clientSecretPath))
{
    Console.Error.WriteLine($"Google OAuth client not found. Save the Desktop app client JSON as {clientSecretPath} (see README.md).");
    return 1;
}

try
{
    using var comfy = new HttpClient { BaseAddress = new Uri(options.ComfyUrl), Timeout = TimeSpan.FromMinutes(2) };
    comfy.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");

    // Signing in first, so the browser prompt doesn't wait until after a long render.
    var drive = await ConnectDriveAsync();
    var folderId = await FindFolderAsync(drive, options.DriveFolder);
    Console.WriteLine($"Drive folder '{options.DriveFolder}' found.");

    var startName = await UploadImageAsync(comfy, options.StartImage, $"{runName}-start");
    var endName = options.EndImage is null ? null : await UploadImageAsync(comfy, options.EndImage, $"{runName}-end");
    var workflow = BuildWorkflow(startName, endName);

    var queuedAt = DateTimeOffset.UtcNow;
    var renderClock = Stopwatch.StartNew();
    var promptId = await QueueAsync(comfy, workflow);
    Console.WriteLine($"Queued as {promptId}. Waiting for ComfyUI…");
    var outputs = await WaitForCompletionAsync(comfy, promptId);
    renderClock.Stop();
    Console.WriteLine($"Rendered in {renderClock.Elapsed:mm\\:ss} (including any time waiting in ComfyUI's queue).");
    var saveVideoOutput = outputs?[SaveVideoNodeId];
    Console.WriteLine($"/history output of node {SaveVideoNodeId}:");
    Console.WriteLine(saveVideoOutput?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "(none)");

    var reportedFileName = ReportedFileName(saveVideoOutput);
    Console.WriteLine(reportedFileName is null
        ? $"/history names no file; looking for the newest '{FileNameStem(workflow)}…' file created after the job was queued."
        : $"Looking for {reportedFileName} on Drive.");
    var driveClock = Stopwatch.StartNew();
    var file = await WaitForDriveFileAsync(drive, folderId, reportedFileName, FileNameStem(workflow), queuedAt);
    driveClock.Stop();
    Console.WriteLine($"On Drive {driveClock.Elapsed:mm\\:ss} after ComfyUI finished: {file.Name} ({FormatSize(file.Size)}).");

    var downloadClock = Stopwatch.StartNew();
    var localPath = Path.Combine(outputDirectory, file.Name);
    await using (var stream = File.Create(localPath))
    {
        var progress = await drive.Files.Get(file.Id).DownloadAsync(stream, ct);
        if (progress.Exception is not null)
            throw progress.Exception;
    }
    downloadClock.Stop();
    Console.WriteLine($"Downloaded from Drive in {downloadClock.Elapsed:mm\\:ss} to {localPath}.");
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Cancelled.");
    return 1;
}
catch (PrototypeException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

JsonObject BuildWorkflow(string startImageName, string? endImageName)
{
    var workflow = JsonNode.Parse(File.ReadAllText(Path.Combine(prototypeDirectory, TemplateFileName))) as JsonObject
        ?? throw new PrototypeException($"{TemplateFileName} is not a ComfyUI API export.");

    var template = Input(workflow, VideoNodeId, "prompt").GetValue<string>();
    if (!template.Contains(PromptPlaceholder))
        throw new PrototypeException($"The prompt template in {TemplateFileName} has no '{PromptPlaceholder}' to replace.");

    SetInput(workflow, FirstFrameNodeId, "image", startImageName);
    if (endImageName is null)
    {
        workflow.Remove(LastFrameNodeId);
        Inputs(workflow, VideoNodeId).Remove("last_frame");
    }
    else
    {
        SetInput(workflow, LastFrameNodeId, "image", endImageName);
    }
    SetInput(workflow, VideoNodeId, "prompt", template.Replace(PromptPlaceholder, options.Prompt));
    SetInput(workflow, SecondsNodeId, "value", options.Seconds);
    SetInput(workflow, SeedNodeId, "seed", Random.Shared.NextInt64(long.MaxValue));
    SetInput(workflow, LightningLoraNodeId, "value", !options.FullQuality);
    return workflow;
}

// "video/MiniMax_H3" saves as video/MiniMax_H3_00001_.mp4, MiniMax_H3_00002_.mp4, ...
static string FileNameStem(JsonObject workflow) =>
    Input(workflow, SaveVideoNodeId, "filename_prefix").GetValue<string>().Split('/').Last();

static string? ReportedFileName(JsonNode? saveVideoOutput) =>
    (saveVideoOutput as JsonObject ?? [])
        .SelectMany(entry => entry.Value as JsonArray ?? [])
        .OfType<JsonObject>()
        .Select(item => item["filename"]?.GetValue<string>())
        .FirstOrDefault(name => !string.IsNullOrEmpty(name));

static JsonObject Inputs(JsonObject workflow, string nodeId) =>
    workflow[nodeId]?["inputs"] as JsonObject ?? throw new PrototypeException($"Node '{nodeId}' is missing from the workflow.");

static JsonNode Input(JsonObject workflow, string nodeId, string inputName) =>
    Inputs(workflow, nodeId)[inputName] ?? throw new PrototypeException($"Node '{nodeId}' has no input '{inputName}'.");

static void SetInput(JsonObject workflow, string nodeId, string inputName, JsonNode value)
{
    Input(workflow, nodeId, inputName);
    Inputs(workflow, nodeId)[inputName] = value;
}

async Task<string> UploadImageAsync(HttpClient comfy, string path, string uploadName)
{
    using var form = new MultipartFormDataContent();
    var file = new ByteArrayContent(await File.ReadAllBytesAsync(path, ct));
    file.Headers.ContentType = new MediaTypeHeaderValue(Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        _ => "image/png",
    });
    form.Add(file, "image", uploadName + Path.GetExtension(path));

    using var response = await comfy.PostAsync("/upload/image", form, ct);
    var answer = await ReadJsonAsync(response, "POST /upload/image");
    var name = answer?["name"]?.GetValue<string>() ?? throw new PrototypeException("ComfyUI's upload answer has no file name.");
    var subfolder = answer["subfolder"]?.GetValue<string>();
    Console.WriteLine($"Uploaded {Path.GetFileName(path)} as {name}.");
    return string.IsNullOrEmpty(subfolder) ? name : $"{subfolder}/{name}";
}

async Task<string> QueueAsync(HttpClient comfy, JsonObject workflow)
{
    var payload = new JsonObject { ["prompt"] = workflow, ["client_id"] = Guid.NewGuid().ToString() };
    using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
    using var response = await comfy.PostAsync("/prompt", content, ct);
    var answer = await ReadJsonAsync(response, "POST /prompt");
    if (answer?["node_errors"] is JsonObject { Count: > 0 } errors)
        Console.WriteLine($"ComfyUI reported node errors: {errors.ToJsonString()}");
    return answer?["prompt_id"]?.GetValue<string>() ?? throw new PrototypeException($"Workflow rejected:\n{answer?.ToJsonString()}");
}

// Same completion and failure checks as Art Studio's ComfyClient.ParseRunStatus.
async Task<JsonNode?> WaitForCompletionAsync(HttpClient comfy, string promptId)
{
    var deadline = DateTime.UtcNow + renderTimeout;
    while (DateTime.UtcNow < deadline)
    {
        using var response = await comfy.GetAsync($"/history/{Uri.EscapeDataString(promptId)}", ct);
        var run = (await ReadJsonAsync(response, "GET /history"))?[promptId];
        if (run?["status"] is JsonObject status)
        {
            var failure = (status["messages"] as JsonArray ?? [])
                .FirstOrDefault(m => m is JsonArray { Count: > 0 } message &&
                                     message[0]?.GetValue<string>() is "execution_error" or "execution_interrupted");
            if (status["status_str"]?.GetValue<string>() == "error" || failure is not null)
                throw new PrototypeException($"Workflow execution failed:\n{(failure ?? status).ToJsonString()}");
            if (status["completed"]?.GetValue<bool>() == true)
                return run["outputs"];
        }
        await Task.Delay(pollInterval, ct);
    }
    throw new PrototypeException($"ComfyUI did not finish within {renderTimeout.TotalMinutes} minutes.");
}

async Task<JsonNode?> ReadJsonAsync(HttpResponseMessage response, string request)
{
    var body = await response.Content.ReadAsStringAsync(ct);
    if (!response.IsSuccessStatusCode)
        throw new PrototypeException($"ComfyUI request failed: {request} ({(int)response.StatusCode})\n{body}");
    try
    {
        return string.IsNullOrWhiteSpace(body) ? null : JsonNode.Parse(body);
    }
    catch (JsonException)
    {
        throw new PrototypeException($"ComfyUI returned non-JSON for {request}:\n{body}");
    }
}

async Task<DriveService> ConnectDriveAsync()
{
    var secrets = (await GoogleClientSecrets.FromFileAsync(clientSecretPath, ct)).Secrets;
    var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
        secrets, [DriveService.Scope.DriveReadonly], "user", ct,
        new FileDataStore(Path.Combine(settingsDirectory, "tokens"), fullPath: true));
    return new DriveService(new BaseClientService.Initializer
    {
        HttpClientInitializer = credential,
        ApplicationName = "Art Studio video prototype",
    });
}

async Task<string> FindFolderAsync(DriveService drive, string folderPath)
{
    var parentId = "root";
    foreach (var name in folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
    {
        var request = drive.Files.List();
        request.Q = $"name = '{EscapeQuery(name)}' and mimeType = '{FolderMimeType}' and '{parentId}' in parents and trashed = false";
        request.Fields = "files(id)";
        var found = (await request.ExecuteAsync(ct)).Files;
        parentId = found.Count switch
        {
            1 => found[0].Id,
            0 => throw new PrototypeException($"Drive folder '{name}' (in '{folderPath}') does not exist."),
            _ => throw new PrototypeException($"Drive has {found.Count} folders named '{name}' (in '{folderPath}')."),
        };
    }
    return parentId;
}

// Drive only reports a checksum once the whole file has arrived. The default prefix reuses names across runs (after
// deletions), so only files created after the job was queued count; a minute of slack covers clock differences.
async Task<Google.Apis.Drive.v3.Data.File> WaitForDriveFileAsync(
    DriveService drive, string folderId, string? fileName, string nameStem, DateTimeOffset queuedAt)
{
    var nameCondition = fileName is null ? $"name contains '{EscapeQuery(nameStem)}'" : $"name = '{EscapeQuery(fileName)}'";
    var createdAfter = (queuedAt - TimeSpan.FromMinutes(1)).ToString("yyyy-MM-ddTHH:mm:ssZ");
    var deadline = DateTime.UtcNow + driveTimeout;
    while (DateTime.UtcNow < deadline)
    {
        var request = drive.Files.List();
        request.Q = $"'{folderId}' in parents and {nameCondition} and createdTime > '{createdAfter}' and trashed = false";
        request.Fields = "files(id, name, size, md5Checksum)";
        request.OrderBy = "createdTime desc";
        var file = (await request.ExecuteAsync(ct)).Files.FirstOrDefault(f => f.Size is not null && f.Md5Checksum is not null);
        if (file is not null)
            return file;
        await Task.Delay(pollInterval, ct);
    }
    throw new PrototypeException(
        $"No complete {fileName ?? nameStem + "…"} file appeared in the Drive folder within {driveTimeout.TotalMinutes} minutes.");
}

static string EscapeQuery(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");

static string FormatSize(long? bytes) => bytes is null ? "unknown size" : $"{bytes.Value / 1024.0 / 1024.0:0.0} MB";

static void PrintHelp() => Console.WriteLine("""
    Renders one MiniMax H3 image-to-video run on ComfyUI and fetches the video from Google Drive instead of through ComfyUI.

    dotnet run VideoViaDrive.cs -- --comfy <url> --start <image> --prompt "<text>" [options]

      --comfy <url>           ComfyUI address (the ngrok URL)
      --start <image>         Start frame (png, jpg or webp)
      --end <image>           End frame; without it the end frame is left out of the workflow
      --prompt "<text>"       Replaces ??? in the workflow's prompt template
      --seconds <n>           Video length in seconds (default 5)
      --full-quality          Turn the Lightning LoRA off: 20 steps instead of 8
      --drive-folder <path>   Drive folder ComfyUI saves videos into (default ArtStudio/video)
      --dry-run               Only write the filled workflow to output/; sends nothing
    """);

sealed record Options(
    string ComfyUrl, string StartImage, string? EndImage, string Prompt, double Seconds, bool FullQuality,
    string DriveFolder, bool DryRun)
{
    public static Options Parse(string[] args)
    {
        var values = new Dictionary<string, string>();
        var flags = new HashSet<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var name = args[i];
            if (name is "--full-quality" or "--dry-run")
                flags.Add(name);
            else if (name.StartsWith("--") && i + 1 < args.Length)
                values[name] = args[++i];
            else
                throw new ArgumentException($"Unexpected argument '{name}'.");
        }

        string Required(string name) => values.GetValueOrDefault(name) ?? throw new ArgumentException($"{name} is required.");
        string ExistingFile(string name, string path) =>
            File.Exists(path) ? Path.GetFullPath(path) : throw new ArgumentException($"{name}: file '{path}' does not exist.");

        var dryRun = flags.Contains("--dry-run");
        var seconds = values.TryGetValue("--seconds", out var text)
            ? double.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed > 0
                ? parsed
                : throw new ArgumentException("--seconds must be a positive number.")
            : 5;
        return new Options(
            dryRun ? values.GetValueOrDefault("--comfy", "") : Required("--comfy"),
            ExistingFile("--start", Required("--start")),
            values.TryGetValue("--end", out var end) ? ExistingFile("--end", end) : null,
            Required("--prompt"),
            seconds,
            flags.Contains("--full-quality"),
            values.GetValueOrDefault("--drive-folder", "ArtStudio/video"),
            dryRun);
    }
}

sealed class PrototypeException(string message) : Exception(message);
