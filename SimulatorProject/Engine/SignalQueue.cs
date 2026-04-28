using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SimulatorProject.Engine;

public class SignalQueue
{
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly SemaphoreSlim _semaphore = new(0);

    public void Enqueue(string signal)
    {
        _queue.Enqueue(signal);
        _semaphore.Release();
    }

    public async Task<bool> WaitForSignalAsync(string expectedSignal, int timeoutMs, CancellationToken ct)
    {
        var deadline = timeoutMs > 0
            ? DateTime.UtcNow.AddMilliseconds(timeoutMs)
            : DateTime.MaxValue;

        while (!ct.IsCancellationRequested)
        {
            var remaining = timeoutMs > 0
                ? (int)(deadline - DateTime.UtcNow).TotalMilliseconds
                : Timeout.Infinite;

            if (remaining <= 0)
                return false;

            bool acquired = await _semaphore.WaitAsync(
                timeoutMs > 0 ? remaining : 100, ct);

            if (!acquired)
            {
                if (timeoutMs > 0)
                    return false;
                continue;
            }

            if (_queue.TryDequeue(out var signal) && signal == expectedSignal)
                return true;

            // Not the expected signal — put it back (best effort)
            if (signal != null)
            {
                _queue.Enqueue(signal);
                _semaphore.Release();
            }
        }

        ct.ThrowIfCancellationRequested();
        return false;
    }

    public void Clear()
    {
        while (_queue.TryDequeue(out _)) { }
        // Drain semaphore
        while (_semaphore.CurrentCount > 0)
            _semaphore.Wait(0);
    }
}
