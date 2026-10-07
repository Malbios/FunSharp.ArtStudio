using ArtStudio.Server.Comfy;
using ArtStudio.Server.Generation;

namespace ArtStudio.Server.Tests;

public class ComfyWorkflowBuilderTests
{
    [Fact]
    public void Build_SetsPromptResolutionAndSeed()
    {
        var builder = ComfyWorkflowBuilder.FromEmbeddedTemplate();

        var workflow = builder.Build("a red fox", Resolutions.Find("Wide")!, 12345);

        Assert.Equal("a red fox", workflow["79:8"]!["inputs"]!["text"]!.GetValue<string>());
        Assert.Equal("1344×768 (7:4) - Wide", workflow["80"]!["inputs"]!["resolution"]!.GetValue<string>());
        Assert.Equal(12345, workflow["79:129"]!["inputs"]!["seed"]!.GetValue<long>());
    }

    [Fact]
    public void Build_ForcesBatchSizeToOne()
    {
        const string template = """
            {
              "79:8": { "inputs": { "text": "" } },
              "80": { "inputs": { "resolution": "" } },
              "79:129": { "inputs": { "seed": 0 } },
              "10": { "inputs": { "batch_size": 4 } }
            }
            """;

        var workflow = new ComfyWorkflowBuilder(template).Build("p", Resolutions.All[0], 1);

        Assert.Equal(1, workflow["10"]!["inputs"]!["batch_size"]!.GetValue<int>());
    }

    [Fact]
    public void Build_ReturnsIndependentCopies()
    {
        var builder = ComfyWorkflowBuilder.FromEmbeddedTemplate();

        var first = builder.Build("first", Resolutions.All[0], 1);
        builder.Build("second", Resolutions.All[0], 2);

        Assert.Equal("first", first["79:8"]!["inputs"]!["text"]!.GetValue<string>());
    }

    [Fact]
    public void Constructor_RejectsUiExport()
    {
        Assert.Throws<InvalidOperationException>(() => new ComfyWorkflowBuilder("""{ "nodes": [] }"""));
    }

    [Fact]
    public void Build_MissingNode_Throws()
    {
        var builder = new ComfyWorkflowBuilder("""{ "1": { "inputs": {} } }""");

        var error = Assert.Throws<InvalidOperationException>(() => builder.Build("p", Resolutions.All[0], 1));
        Assert.Contains("79:8", error.Message);
    }

    [Fact]
    public void SeedGenerator_ProducesPositiveUniqueSeeds()
    {
        var generator = new SeedGenerator();

        var seeds = Enumerable.Range(0, 1000).Select(_ => generator.Next()).ToList();

        Assert.All(seeds, seed => Assert.True(seed >= 0));
        Assert.Equal(seeds.Count, seeds.Distinct().Count());
    }
}
