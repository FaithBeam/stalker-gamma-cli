using Stalker.Gamma.GammaInstallerServices.GammaInstaller;
using Stalker.Gamma.ModDb.Services;
using Stalker.Gamma.Models;

namespace Stalker.Gamma.Tests;

public class GammaInstallerTests
{
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
