using System.Reactive.Disposables;

namespace stalker_gamma_cli.Utilities;

public static class DedicatedThreadSamplerExtensions
{
    /// <summary>
    /// Like <c>Sample(interval).Subscribe(onNext)</c>, but the sampling and <paramref name="onNext"/>
    /// run on a dedicated thread instead of the thread pool.
    /// </summary>
    /// <remarks>
    /// Rx's <c>Sample</c> fires from a thread pool timer. When the pool is saturated with long
    /// synchronous work (7z extraction, hashing, libgit2 checkouts), the timer callback sits in the
    /// queue and progress stops being written. A dedicated thread doesn't compete for pool threads.
    /// </remarks>
    public static IDisposable SubscribeSampledOnDedicatedThread<T>(
        this IObservable<T> source,
        TimeSpan interval,
        Action<T> onNext
    )
        where T : class
    {
        var sampler = new DedicatedThreadSampler<T>(interval, onNext);
        var subscription = source.Subscribe(sampler.Post);
        // stop receiving values before stopping the thread
        return new CompositeDisposable(subscription, sampler);
    }
}

/// <summary>
/// Holds the most recently posted value and hands it to a callback at a fixed interval
/// from its own thread. Values posted between ticks are dropped except for the latest,
/// and nothing is written on a tick where no new value arrived.
/// </summary>
public sealed class DedicatedThreadSampler<T> : IDisposable
    where T : class
{
    private readonly TimeSpan _interval;
    private readonly Action<T> _onNext;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _stop = new();
    private T? _latest;
    private int _disposed;

    public DedicatedThreadSampler(TimeSpan interval, Action<T> onNext)
    {
        _interval = interval;
        _onNext = onNext;
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "progress-sampler",
            // only honoured on Windows; elsewhere the dedicated thread alone is enough
            Priority = ThreadPriority.AboveNormal,
        };
        _thread.Start();
    }

    /// <summary>Records <paramref name="value"/> as the latest value. Safe from any thread.</summary>
    public void Post(T value) => Volatile.Write(ref _latest, value);

    private void Run()
    {
        while (!_stop.Wait(_interval))
        {
            if (Interlocked.Exchange(ref _latest, null) is { } value)
            {
                _onNext(value);
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _stop.Set();
        // onNext disposing its own sampler would otherwise deadlock on Join
        if (Thread.CurrentThread != _thread)
        {
            _thread.Join();
            _stop.Dispose();
        }
    }
}
