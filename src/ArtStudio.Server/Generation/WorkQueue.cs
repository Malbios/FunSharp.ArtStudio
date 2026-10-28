using System.Threading.Channels;

namespace ArtStudio.Server.Generation;

/// <summary>Wakes a background worker and lets the items it is working on be cancelled by id.</summary>
public abstract class WorkQueue
{
    private readonly Channel<bool> _wakeSignal =
        Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    private readonly Lock _lock = new();
    private readonly Dictionary<int, CancellationTokenSource> _running = [];

    public void Wake() => _wakeSignal.Writer.TryWrite(true);

    public async Task WaitForWakeAsync(CancellationToken ct) => await _wakeSignal.Reader.ReadAsync(ct);

    public CancellationToken BeginRun(int id, CancellationToken stoppingToken)
    {
        lock (_lock)
        {
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _running[id] = cancellation;
            return cancellation.Token;
        }
    }

    public void EndRun(int id)
    {
        lock (_lock)
        {
            if (_running.Remove(id, out var cancellation))
                cancellation.Dispose();
        }
    }

    public bool CancelRunning(int id)
    {
        lock (_lock)
        {
            if (!_running.TryGetValue(id, out var cancellation))
                return false;
            cancellation.Cancel();
            return true;
        }
    }
}
