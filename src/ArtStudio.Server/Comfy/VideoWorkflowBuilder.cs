using System.Text.Json.Nodes;

namespace ArtStudio.Server.Comfy;

public sealed record VideoWorkflowInputs(
    string StartImageName,
    string? EndImageName,
    string Prompt,
    double Seconds,
    double Megapixels,
    bool Turbo,
    long Seed,
    string FileNamePrefix);

/// <summary>Fills the MiniMax H3 image-to-video workflow, an API export of its first-and-last-frame variant.</summary>
public sealed class VideoWorkflowBuilder
{
    private const string EmbeddedTemplateName = "ArtStudio.Server.Comfy.minimax_h3_i2v.json";
    public const string SaveVideoNodeId = "92";
    public const string FirstFrameNodeId = "114";
    public const string LastFrameNodeId = "128";
    public const string VideoNodeId = "135:105:104";
    public const string SecondsNodeId = "135:279";
    public const string MegapixelsNodeId = "135:290";
    public const string SeedNodeId = "135:223:139";
    public const string TurboNodeId = "135:105:218:126";

    private readonly string _templateJson;

    public VideoWorkflowBuilder(string templateJson)
    {
        _templateJson = templateJson;
        PromptTemplate = Input(ParseTemplate(), VideoNodeId, "prompt").GetValue<string>();
    }

    /// <summary>The prompt as the workflow ships it; "???" marks where the description of the motion goes.</summary>
    public string PromptTemplate { get; }

    public static VideoWorkflowBuilder FromEmbeddedTemplate()
    {
        using var stream = typeof(VideoWorkflowBuilder).Assembly.GetManifestResourceStream(EmbeddedTemplateName)
            ?? throw new InvalidOperationException($"Embedded workflow '{EmbeddedTemplateName}' is missing.");
        using var reader = new StreamReader(stream);
        return new VideoWorkflowBuilder(reader.ReadToEnd());
    }

    public JsonObject Build(VideoWorkflowInputs inputs)
    {
        var workflow = ParseTemplate();
        SetInput(workflow, FirstFrameNodeId, "image", inputs.StartImageName);
        if (inputs.EndImageName is null)
        {
            // The same workflow without an end frame is exactly the "first frame only" variant.
            workflow.Remove(LastFrameNodeId);
            Inputs(workflow, VideoNodeId).Remove("last_frame");
        }
        else
        {
            SetInput(workflow, LastFrameNodeId, "image", inputs.EndImageName);
        }
        SetInput(workflow, VideoNodeId, "prompt", inputs.Prompt);
        SetInput(workflow, SecondsNodeId, "value", inputs.Seconds);
        SetInput(workflow, MegapixelsNodeId, "value", inputs.Megapixels);
        SetInput(workflow, SeedNodeId, "seed", inputs.Seed);
        SetInput(workflow, TurboNodeId, "value", inputs.Turbo);
        SetInput(workflow, SaveVideoNodeId, "filename_prefix", inputs.FileNamePrefix);
        return workflow;
    }

    private JsonObject ParseTemplate()
    {
        if (JsonNode.Parse(_templateJson) is not JsonObject workflow || workflow.Count == 0 || workflow.ContainsKey("nodes"))
            throw new InvalidOperationException("Expected a ComfyUI API export (File -> Export (API)).");
        return workflow;
    }

    private static JsonObject Inputs(JsonObject workflow, string nodeId) =>
        workflow[nodeId]?["inputs"] as JsonObject
        ?? throw new InvalidOperationException($"Node '{nodeId}' does not exist in the video workflow.");

    private static JsonNode Input(JsonObject workflow, string nodeId, string inputName) =>
        Inputs(workflow, nodeId)[inputName]
        ?? throw new InvalidOperationException($"Node '{nodeId}' does not contain input '{inputName}'.");

    private static void SetInput(JsonObject workflow, string nodeId, string inputName, JsonNode value)
    {
        Input(workflow, nodeId, inputName);
        Inputs(workflow, nodeId)[inputName] = value;
    }
}
