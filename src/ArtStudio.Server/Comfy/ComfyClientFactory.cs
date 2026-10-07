namespace ArtStudio.Server.Comfy;

public sealed class ComfyClientFactory(IHttpClientFactory httpClientFactory)
{
    public ComfyClient Create(string serverUrl)
    {
        var http = httpClientFactory.CreateClient(ComfyClient.HttpClientName);
        http.BaseAddress = new Uri(serverUrl.TrimEnd('/') + "/");
        http.Timeout = TimeSpan.FromSeconds(60);
        http.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");
        return new ComfyClient(http);
    }
}
