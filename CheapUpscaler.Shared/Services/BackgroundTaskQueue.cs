using System.Threading.Channels;

namespace CheapUpscaler.Shared.Services;

/// <summary>
/// Interface for background task queue
/// </summary>
public interface IBackgroundTaskQueue
{
    ValueTask QueueBackgroundWorkItemAsync(Func<CancellationToken, ValueTask> workItem, CancellationToken cancellationToken = default);
    ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Channel-based background task queue for processing upscale jobs.
/// Unbounded on purpose: the items are tiny closures and a bounded channel deadlocked
/// startup (re-queuing more pending jobs than the capacity before the consumer ran).
/// Actual parallelism is limited by the queue service's semaphore, not by this channel.
/// </summary>
public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<CancellationToken, ValueTask>> _queue =
        Channel.CreateUnbounded<Func<CancellationToken, ValueTask>>();

    public async ValueTask QueueBackgroundWorkItemAsync(Func<CancellationToken, ValueTask> workItem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        await _queue.Writer.WriteAsync(workItem, cancellationToken);
    }

    public async ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}
