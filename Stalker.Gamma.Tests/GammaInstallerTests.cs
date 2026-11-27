using Stalker.Gamma.GammaInstallerServices.GammaInstaller;
using Stalker.Gamma.ModDb.Services;
using Stalker.Gamma.Models;

namespace Stalker.Gamma.Tests;

public class GammaInstallerTests
{
    [Fact]
    public async Task Batch_reports_the_failure_that_cancelled_the_rest()
    {
        var otherWorkCancelled = false;

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GammaInstaller.RunBatchAsync(
                CancellationToken.None,
                [
                    async ct =>
                    {
                        try
                        {
                            await Task.Delay(Timeout.Infinite, ct);
                        }
                        catch (OperationCanceledException)
                        {
                            otherWorkCancelled = true;
                            throw;
                        }
                    },
                    async _ =>
                    {
                        await Task.Yield();
                        throw new InvalidOperationException("special repo failed");
                    },
                ]
            )
        );

        Assert.Equal("special repo failed", e.Message);
        Assert.True(otherWorkCancelled);
    }

    [Fact]
    public async Task Batch_completes_when_all_work_succeeds()
    {
        var done = 0;

        await GammaInstaller.RunBatchAsync(
            CancellationToken.None,
            Enumerable
                .Range(0, 4)
                .Select<int, Func<CancellationToken, Task>>(_ =>
                    async _ =>
                    {
                        await Task.Yield();
                        Interlocked.Increment(ref done);
                    }
                )
        );

        Assert.Equal(4, done);
    }

    [Fact]
    public async Task Cancelled_batch_throws_a_cancellation()
    {
        using var cts = new CancellationTokenSource();

        var batch = GammaInstaller.RunBatchAsync(
            cts.Token,
            [ct => Task.Delay(Timeout.Infinite, ct)]
        );
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => batch);
    }

    [Fact]
    public async Task Every_broken_addon_is_retried_and_all_failures_are_reported()
    {
        var first = new FakeRecord("First") { FailDownload = true };
        var second = new FakeRecord("Second");
        var third = new FakeRecord("Third") { FailExtract = true };

        var e = await Assert.ThrowsAsync<AddonsFailedException>(() =>
            GammaInstaller.RetryBrokenAddonsAsync(
                [first, second, third],
                minimal: false,
                CancellationToken.None
            )
        );

        // the first failure didn't stop the others from being retried
        Assert.True(second.Extracted);
        Assert.Equal(["First", "Third"], e.AddonNames);
        Assert.Contains("First: download failed", e.Message);
        Assert.Contains("Third: extract failed", e.Message);
        Assert.Equal(2, e.InnerExceptions.Count);
    }

    [Fact]
    public async Task Retried_addons_that_succeed_dont_throw()
    {
        var addon = new FakeRecord("Addon");

        await GammaInstaller.RetryBrokenAddonsAsync([addon], minimal: false, default);

        Assert.True(addon.Extracted);
    }

    [Fact]
    public async Task Cloudflare_challenge_stops_the_retries()
    {
        var challenged = new FakeRecord("Challenged")
        {
            DownloadError = new CloudflareChallengeException("challenged"),
        };
        var next = new FakeRecord("Next");

        await Assert.ThrowsAsync<CloudflareChallengeException>(() =>
            GammaInstaller.RetryBrokenAddonsAsync([challenged, next], minimal: false, default)
        );

        Assert.False(next.Extracted);
    }

    [Fact]
    public async Task Waiting_for_the_server_returns_once_it_is_ready()
    {
        var checks = 0;

        await GammaInstaller.WaitUntilReadyAsync(
            () => Task.FromResult(++checks == 3),
            "http://localhost:8000",
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMilliseconds(1),
            default
        );

        Assert.Equal(3, checks);
    }

    [Fact]
    public async Task Waiting_for_a_server_that_never_gets_ready_times_out()
    {
        var e = await Assert.ThrowsAsync<TimeoutException>(() =>
            GammaInstaller.WaitUntilReadyAsync(
                () => Task.FromResult(false),
                "http://localhost:8000",
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(10),
                default
            )
        );

        Assert.Contains("http://localhost:8000", e.Message);
    }

    [Fact]
    public async Task Cancelling_the_wait_for_the_server_throws_a_cancellation()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            GammaInstaller.WaitUntilReadyAsync(
                () => Task.FromResult(false),
                "http://localhost:8000",
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMilliseconds(10),
                cts.Token
            )
        );
    }

    private class FakeRecord(string name) : IDownloadableRecord
    {
        public bool FailDownload { get; init; }
        public bool FailExtract { get; init; }
        public Exception? DownloadError { get; init; }
        public bool Extracted { get; private set; }

        public string Name => name;
        public string ArchiveName => $"{name}.zip";
        public string DownloadPath => Path.Join(Path.GetTempPath(), "does-not-exist", ArchiveName);
        public bool Downloaded => true;

        public Task DownloadAsync(CancellationToken cancellationToken) =>
            DownloadError is not null ? Task.FromException(DownloadError)
            : FailDownload ? Task.FromException(new IOException("download failed"))
            : Task.CompletedTask;

        public Task ExtractAsync(CancellationToken cancellationToken)
        {
            if (FailExtract)
            {
                return Task.FromException(new IOException("extract failed"));
            }
            Extracted = true;
            return Task.CompletedTask;
        }
    }
}
