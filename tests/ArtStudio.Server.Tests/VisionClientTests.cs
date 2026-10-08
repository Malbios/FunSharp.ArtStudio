using System.Net;
using ArtStudio.Server.Tests.Fakes;
using ArtStudio.Server.Vision;
using Microsoft.Extensions.Configuration;

namespace ArtStudio.Server.Tests;

public sealed class VisionClientTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0];

    private readonly FakeVisionServer _server = new();

    private VisionClient CreateClient(string? serverUrl = null)
    {
        var settings = new Dictionary<string, string?> { [VisionClient.ServerUrlKey] = serverUrl };
        return new VisionClient(new SingleHandlerFactory(_server), new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    }

    private Task<VisionAnswer> DescribeAsync(VisionClient? client = null) =>
        (client ?? CreateClient()).DescribeAsync("secret-key", Jpeg, "image/jpeg", "Describe it.", CancellationToken.None);

    [Fact]
    public async Task SendsOpenAiStyleChatRequestWithTheImage()
    {
        await DescribeAsync();

        var request = Assert.Single(_server.Requests);
        Assert.Equal("http://127.0.0.1:18080/v1/chat/completions", request.Uri.ToString());
        Assert.Equal("Bearer secret-key", request.Authorization);
        Assert.Equal("vision", request.Body["model"]!.GetValue<string>());
        Assert.Equal(0.2, request.Body["temperature"]!.GetValue<double>());
        Assert.Equal(4096, request.Body["max_tokens"]!.GetValue<int>());
        Assert.False(request.Body["stream"]!.GetValue<bool>());

        var message = request.Body["messages"]![0]!;
        Assert.Equal("user", message["role"]!.GetValue<string>());
        Assert.Equal("text", message["content"]![0]!["type"]!.GetValue<string>());
        Assert.Equal("Describe it.", message["content"]![0]!["text"]!.GetValue<string>());
        Assert.Equal("image_url", message["content"]![1]!["type"]!.GetValue<string>());
        Assert.Equal($"data:image/jpeg;base64,{Convert.ToBase64String(Jpeg)}",
            message["content"]![1]!["image_url"]!["url"]!.GetValue<string>());
    }

    [Fact]
    public async Task UsesTheConfiguredServerUrl()
    {
        await DescribeAsync(CreateClient("http://vision.test:9000/"));

        Assert.Equal("http://vision.test:9000/v1/chat/completions", _server.Requests.Single().Uri.ToString());
    }

    [Fact]
    public async Task ReturnsTheAnswer_AndWhetherItWasCutOff()
    {
        _server.Answer = "  A fox.\n\nIn snow.  ";
        Assert.Equal(new VisionAnswer("A fox.\n\nIn snow.", false), await DescribeAsync());

        _server.FinishReason = "length";
        Assert.True((await DescribeAsync()).Truncated);
    }

    [Fact]
    public async Task RejectedKey_ExplainsWhereToFixIt()
    {
        _server.Status = HttpStatusCode.Unauthorized;

        var error = await Assert.ThrowsAsync<VisionException>(() => DescribeAsync());

        Assert.Contains("API key", error.Message);
    }

    [Fact]
    public async Task UnreachableServer_MentionsTheTunnel()
    {
        _server.Unreachable = true;

        var error = await Assert.ThrowsAsync<VisionException>(() => DescribeAsync());

        Assert.Contains("http://127.0.0.1:18080", error.Message);
        Assert.Contains("SSH tunnel", error.Message);
    }

    [Fact]
    public async Task ServerError_IncludesStatus()
    {
        _server.Status = HttpStatusCode.InternalServerError;

        var error = await Assert.ThrowsAsync<VisionException>(() => DescribeAsync());

        Assert.Contains("500", error.Message);
    }

    [Fact]
    public async Task EmptyAnswer_IsAnError()
    {
        _server.Answer = "   ";

        await Assert.ThrowsAsync<VisionException>(() => DescribeAsync());
    }

    private sealed class SingleHandlerFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
