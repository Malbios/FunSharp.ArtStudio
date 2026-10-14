using ArtStudio.Server.Vision;

namespace ArtStudio.Server.Tests;

public sealed class PostSuggestionTurnsTests
{
    private readonly PostSuggestionTurns _turns = new();

    [Fact]
    public async Task ClickedWaiter_GoesBeforeAnEarlierBackgroundWaiter()
    {
        var running = await _turns.BackgroundAsync(CancellationToken.None);
        var background = _turns.BackgroundAsync(CancellationToken.None);
        var clicked = _turns.ClickedAsync(CancellationToken.None);
        Assert.False(background.IsCompleted || clicked.IsCompleted);

        running.Dispose();
        var clickedTurn = await clicked.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(background.IsCompleted);

        clickedTurn.Dispose();
        (await background.WaitAsync(TimeSpan.FromSeconds(5))).Dispose();
    }

    [Fact]
    public async Task CancelledWaiter_LeavesTheLine()
    {
        var running = await _turns.ClickedAsync(CancellationToken.None);
        using var cancel = new CancellationTokenSource();
        var cancelled = _turns.ClickedAsync(cancel.Token);
        var next = _turns.BackgroundAsync(CancellationToken.None);

        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        running.Dispose();

        (await next.WaitAsync(TimeSpan.FromSeconds(5))).Dispose();
        (await _turns.ClickedAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5))).Dispose();
    }
}
