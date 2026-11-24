using System.Reactive.Disposables;
using Stalker.Gamma.GammaInstallerServices;

namespace stalker_gamma_cli.Utilities;

public static class DedicatedThreadSamplerExtensions
{
    /// <summary>
    /// Like <c>GroupBy(keySelector).SelectMany(g => g.Sample(interval)).Subscribe(onNext)</c>, but the
    /// sampling and <paramref name="onNext"/> run on a dedicated thread instead of the thread pool.
    /// Each key is throttled on its own, so a busy key can't crowd out the others.
    /// </summary>
    /// <remarks>
    /// Rx's <c>Sample</c> fires from a thread pool timer. When the pool is saturated with long
    /// synchronous work (7z extraction, hashing, libgit2 checkouts), the timer callback sits in the
    /// queue and progress stops being written. A dedicated thread doesn't compete for pool threads.
    /// </remarks>
    public static IDisposable SubscribeSampledOnDedicatedThread<T, TKey>(
        this IObservable<T> source,
        TimeSpan interval,
        Func<T, TKey> keySelector,
        Action<T> onNext
    )
        where TKey : notnull
    {
        var sampler = new DedicatedThreadSampler<T, TKey>(interval, keySelector, onNext);
        var subscription = source.Subscribe(sampler.Post);
        // stop receiving values before stopping the thread
        return new CompositeDisposable(subscription, sampler);
    }

    /// <summary>
    /// Samples install progress separately for each download, extract, etc. so that every one
    /// running in parallel gets written at <paramref name="interval"/>.
    /// </summary>
    public static IDisposable SubscribeSampledPerOperationOnDedicatedThread(
        this IObservable<GammaProgress.GammaInstallProgressEventArgs> source,
        TimeSpan interval,
        Action<GammaProgress.GammaInstallProgressEventArgs> onNext
    ) => source.SubscribeSampledOnDedicatedThread(interval, ProgressKey, onNext);

    private static (GammaProgressType, string, string, string) ProgressKey(
        GammaProgress.GammaInstallProgressEventArgs e
    ) => (e.ProgressType, e.Name, e.ArchiveName, e.DownloadPath);
}

/// <summary>
/// Holds the most recently posted value for each key and hands them to a callback at a fixed
/// interval from its own thread. Values posted between ticks are dropped except for the latest
/// one per key, and nothing is written for a key on a tick where no new value arrived for it.
/// </summary>
public sealed class DedicatedThreadSampler<T, TKey> : IDisposable
    where TKey : notnull
{
    private readonly TimeSpan _interval;
    private readonly Func<T, TKey> _keySelector;
    private readonly Action<T> _onNext;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _stop = new();
    private readonly Lock _gate = new();

    // only keys that received a value since the last tick, so it doesn't grow with the install
    private Dictionary<TKey, T> _pending = [];
    private Dictionary<TKey, T> _spare = [];
    private int _disposed;

    public DedicatedThreadSampler(TimeSpan interval, Func<T, TKey> keySelector, Action<T> onNext)
    {
        _interval = interval;
        _keySelector = keySelector;
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

    /// <summary>
    /// Records <paramref name="value"/> as the latest value for its key. Safe from any thread.
    /// </summary>
    public void Post(T value)
    {
        var key = _keySelector(value);
        lock (_gate)
        {
            _pending[key] = value;
        }
    }

    private void Run()
    {
        while (!_stop.Wait(_interval))
        {
            Dictionary<TKey, T> batch;
            lock (_gate)
            {
                if (_pending.Count == 0)
                {
                    continue;
                }

                batch = _pending;
                _pending = _spare;
            }

            // in the order each key first reported since the last tick
            foreach (var value in batch.Values)
            {
                _onNext(value);
            }

            batch.Clear();
            _spare = batch;
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
