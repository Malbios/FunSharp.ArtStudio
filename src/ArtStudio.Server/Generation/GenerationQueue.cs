using System.Threading.Channels;

namespace ArtStudio.Server.Generation;

public sealed class GenerationQueue
{
    private readonly Channel<bool> _wakeSignal =
        Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    private readonly Lock _lock = new();
    private int? _runningJobId;
    private CancellationTokenSource? _runningCancellation;

    public void Wake() => _wakeSignal.Writer.TryWrite(true);

    public async Task WaitForWakeAsync(CancellationToken ct) => await _wakeSignal.Reader.ReadAsync(ct);

    public CancellationToken BeginRun(int jobId, CancellationToken stoppingToken)
    {
        lock (_lock)
        {
            _runningJobId = jobId;
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
            _runningJobId = null;
        }
    }

    public bool CancelRunning(int jobId)
    {
        lock (_lock)
        {
            if (_runningJobId != jobId || _runningCancellation is null)
                return false;
            _runningCancellation.Cancel();
            return true;
        }
    }
}
