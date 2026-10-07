using System.Text.Json.Nodes;
using ArtStudio.Server.Generation;

namespace ArtStudio.Server.Comfy;

public sealed class ComfyWorkflowBuilder
{
    private const string EmbeddedTemplateName = "ArtStudio.Server.Comfy.krea2_t2i.json";
    private const string PromptNodeId = "79:8";
    private const string ResolutionNodeId = "80";
    private const string SeedNodeId = "79:129";

    private readonly string _templateJson;

    public ComfyWorkflowBuilder(string templateJson)
    {
        _templateJson = templateJson;
        ParseTemplate();
    }

    public static ComfyWorkflowBuilder FromEmbeddedTemplate()
    {
        using var stream = typeof(ComfyWorkflowBuilder).Assembly.GetManifestResourceStream(EmbeddedTemplateName)
            ?? throw new InvalidOperationException($"Embedded workflow '{EmbeddedTemplateName}' is missing.");
        using var reader = new StreamReader(stream);
        return new ComfyWorkflowBuilder(reader.ReadToEnd());
    }

    public JsonObject Build(string prompt, ResolutionPreset resolution, long seed)
    {
        var workflow = ParseTemplate();
        SetInput(workflow, PromptNodeId, "text", prompt);
        SetInput(workflow, ResolutionNodeId, "resolution", resolution.ComfyLabel);
        SetInput(workflow, SeedNodeId, "seed", seed);
        ForceSingleImageBatches(workflow);
        return workflow;
    }

    private JsonObject ParseTemplate()
    {
        if (JsonNode.Parse(_templateJson) is not JsonObject workflow || workflow.Count == 0 || workflow.ContainsKey("nodes"))
            throw new InvalidOperationException("Expected a ComfyUI API export (File -> Export (API)).");
        return workflow;
    }

    private static void SetInput(JsonObject workflow, string nodeId, string inputName, JsonNode value)
    {
        if (workflow[nodeId] is not JsonObject node)
            throw new InvalidOperationException($"Node '{nodeId}' does not exist in the workflow.");
        if (node["inputs"] is not JsonObject inputs || !inputs.ContainsKey(inputName))
            throw new InvalidOperationException($"Node '{nodeId}' does not contain input '{inputName}'.");
        inputs[inputName] = value;
    }

    // Independent runs with their own seeds, rather than a latent batch sharing one seed.
    private static void ForceSingleImageBatches(JsonObject workflow)
    {
        foreach (var (_, node) in workflow)
        {
            if (node?["inputs"] is JsonObject inputs && inputs.ContainsKey("batch_size"))
                inputs["batch_size"] = 1;
        }
    }
}
