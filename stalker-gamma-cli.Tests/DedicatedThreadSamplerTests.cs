using System.Collections.Concurrent;
using stalker_gamma_cli.Utilities;

namespace stalker_gamma_cli.Tests;

public class DedicatedThreadSamplerTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void Writes_the_latest_value_of_every_key_each_tick()
    {
        var written = new BlockingCollection<(string Key, int Value)>();
        var blocked = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        using var sampler = new DedicatedThreadSampler<(string Key, int Value), string>(
            Interval,
            x => x.Key,
            x =>
            {
                if (x.Key == "hold")
                {
                    // keep the sampler thread here so the posts below all land in one tick
                    blocked.Set();
                    release.Wait(Timeout);
                    return;
                }
                written.Add(x);
            }
        );

        sampler.Post(("hold", 0));
        Assert.True(blocked.Wait(Timeout));

        sampler.Post(("a", 1));
        sampler.Post(("b", 1));
        sampler.Post(("a", 2));
        sampler.Post(("c", 1));
        sampler.Post(("b", 2));
        release.Set();

        Assert.Equal([("a", 2), ("b", 2), ("c", 1)], Take(written, 3));
        Assert.False(written.TryTake(out _, Interval * 4));
    }

    [Fact]
    public void Writes_nothing_for_a_key_with_no_new_value()
    {
        var written = new BlockingCollection<(string Key, int Value)>();
        using var sampler = new DedicatedThreadSampler<(string Key, int Value), string>(
            Interval,
            x => x.Key,
            written.Add
        );

        sampler.Post(("a", 1));
        Assert.Equal([("a", 1)], Take(written, 1));

        sampler.Post(("b", 1));
        Assert.Equal([("b", 1)], Take(written, 1));

        Assert.False(written.TryTake(out _, Interval * 4));
    }

    private static List<T> Take<T>(BlockingCollection<T> collection, int count)
    {
        List<T> taken = [];
        for (var i = 0; i < count; i++)
        {
            Assert.True(collection.TryTake(out var item, Timeout), "timed out waiting for a value");
            taken.Add(item);
        }
        return taken;
    }
}
