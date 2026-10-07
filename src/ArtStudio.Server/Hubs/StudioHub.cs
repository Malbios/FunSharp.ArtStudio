using Microsoft.AspNetCore.SignalR;

namespace ArtStudio.Server.Hubs;

public sealed class StudioHub : Hub;

public sealed class StudioNotifier(IHubContext<StudioHub> hub)
{
    public Task JobUpdated(int jobId, int setId) =>
        hub.Clients.All.SendAsync("JobUpdated", new { jobId, setId });

    public Task ImageAdded(int imageId, int setId) =>
        hub.Clients.All.SendAsync("ImageAdded", new { imageId, setId });

    public Task JobCompleted(int jobId, int setId, string prompt) =>
        hub.Clients.All.SendAsync("JobCompleted", new { jobId, setId, prompt });

    public Task QueueStateChanged(bool paused) =>
        hub.Clients.All.SendAsync("QueueStateChanged", new { paused });

    public Task QueueEmpty() =>
        hub.Clients.All.SendAsync("QueueEmpty");
}
