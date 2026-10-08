using System.Threading.Channels;

namespace ArtStudio.Server.Generation;

/// <summary>Wakes a background worker and lets the one item it is working on be cancelled by id.</summary>
public abstract class WorkQueue
{
    private readonly Channel<bool> _wakeSignal =
        Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    private readonly Lock _lock = new();
    private int? _runningId;
    private CancellationTokenSource? _runningCancellation;

    public void Wake() => _wakeSignal.Writer.TryWrite(true);

    public async Task WaitForWakeAsync(CancellationToken ct) => await _wakeSignal.Reader.ReadAsync(ct);

    public CancellationToken BeginRun(int id, CancellationToken stoppingToken)
    {
        lock (_lock)
        {
            _runningId = id;
            _runningCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            return _runningCancellation.Token;
        }
    }

    public void EndRun()
    {
        lock (_lock)
        {
            _runningCancellation?.Dispose();
            _runningCancellation = null;
            _runningId = null;
        }
    }

    public bool CancelRunning(int id)
    {
        lock (_lock)
        {
            if (_runningId != id || _runningCancellation is null)
                return false;
            _runningCancellation.Cancel();
            return true;
        }
    }
}
