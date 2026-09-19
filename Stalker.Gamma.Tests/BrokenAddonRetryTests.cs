using Stalker.Gamma.GammaInstallerServices.GammaInstaller;
using Stalker.Gamma.Models;

namespace Stalker.Gamma.Tests;

public class BrokenAddonRetryTests
{
    [Fact]
    public async Task RetryAsync_when_download_fails_includes_every_remaining_addon_name_and_link()
    {
        var failed = new FakeAddon
        {
            Name = "Broken Weather",
            DownloadLink = "https://www.moddb.com/downloads/start/111",
            OnDownload = () => throw new InvalidOperationException("mirror failed"),
        };
        var leftoverA = new FakeAddon
        {
            Name = "Night Vision",
            DownloadLink = "https://www.moddb.com/downloads/start/222",
            OnDownload = () => throw new InvalidOperationException("should not be reached"),
        };
        var leftoverB = new FakeAddon
        {
            Name = "Sound Overhaul",
            DownloadLink = "https://github.com/example/sound/releases/download/1.0/sound.zip",
        };

        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            BrokenAddonRetry.RetryAsync([failed, leftoverA, leftoverB])
        );

        Assert.Contains("Broken Weather", ex.Message);
        Assert.Contains("https://www.moddb.com/downloads/start/111", ex.Message);
        Assert.Contains("Night Vision", ex.Message);
        Assert.Contains("https://www.moddb.com/downloads/start/222", ex.Message);
        Assert.Contains("Sound Overhaul", ex.Message);
        Assert.Contains(
            "https://github.com/example/sound/releases/download/1.0/sound.zip",
            ex.Message
        );
    }

    [Fact]
    public async Task RetryAsync_omits_already_fetched_addons_from_remaining_list()
    {
        var done = new FakeAddon
        {
            Name = "Already Fetched",
            DownloadLink = "https://example.com/done",
        };
        var failed = new FakeAddon
        {
            Name = "Broken Weather",
            DownloadLink = "https://www.moddb.com/downloads/start/111",
            OnDownload = () => throw new InvalidOperationException("mirror failed"),
        };
        var leftover = new FakeAddon
        {
            Name = "Night Vision",
            DownloadLink = "https://www.moddb.com/downloads/start/222",
        };

        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            BrokenAddonRetry.RetryAsync([done, failed, leftover])
        );

        Assert.DoesNotContain("Already Fetched", ex.Message);
        Assert.DoesNotContain("https://example.com/done", ex.Message);
        Assert.Contains("Broken Weather", ex.Message);
        Assert.Contains("https://www.moddb.com/downloads/start/111", ex.Message);
        Assert.Contains("Night Vision", ex.Message);
        Assert.Contains("https://www.moddb.com/downloads/start/222", ex.Message);
    }

    private sealed class FakeAddon : IDownloadableRecord
    {
        public required string Name { get; init; }
        public string ArchiveName => Name;
        public string DownloadPath => Name;
        public string? DownloadLink { get; init; }
        public bool Downloaded { get; private set; }
        public Action? OnDownload { get; init; }

        public Task DownloadAsync(CancellationToken cancellationToken)
        {
            OnDownload?.Invoke();
            Downloaded = true;
            return Task.CompletedTask;
        }

        public Task ExtractAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
