using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.Models;
using Stalker.Gamma.Services;
using Stalker.Gamma.Utilities;
using Xunit;

namespace Stalker.Gamma.Tests;

public sealed class CachedAddonArchiveReuseTests
{
    [Fact]
    public async Task ReusesMatchingArchiveInConfiguredCacheWithoutRedownloading()
    {
        using var fx = AddonLayoutFixture.Create();
        var archive = fx.WriteArchive(fx.CacheDirectory);
        var record = fx.CreateGithubRecord(archive.Md5);

        await record.DownloadAsync(TestContext.Current.CancellationToken);

        AssertDidNotRedownload(record, fx, archive);
        Assert.Equal(archive.Path, record.DownloadPath);
        Assert.False(File.Exists(Path.Join(fx.DownloadsDirectory, archive.Name)));
    }

    [Fact]
    public async Task ReusesMatchingArchiveInDownloadsWithoutRedownloading()
    {
        using var fx = AddonLayoutFixture.Create();
        var archive = fx.WriteArchive(fx.DownloadsDirectory);
        var record = fx.CreateGithubRecord(archive.Md5);

        await record.DownloadAsync(TestContext.Current.CancellationToken);

        AssertDidNotRedownload(record, fx, archive);
        Assert.Equal(archive.Path, record.DownloadPath);
    }

    [Fact]
    public async Task ReusesMatchingArchiveWhenDownloadsIsSymlinkToCache()
    {
        using var fx = AddonLayoutFixture.Create(symlinkDownloadsToCache: true);
        var archive = fx.WriteArchive(fx.CacheDirectory);
        var record = fx.CreateGithubRecord(archive.Md5);

        await record.DownloadAsync(TestContext.Current.CancellationToken);

        AssertDidNotRedownload(record, fx, archive);
        Assert.True(File.Exists(Path.Join(fx.DownloadsDirectory, archive.Name)));
    }

    [Fact]
    public void ReplacesEmptyDownloadsDirectoryWithSymlinkToCache()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("In-process symlink creation is Unix-only.");
            return;
        }

        using var fx = AddonLayoutFixture.Create();
        Assert.False(
            new DirectoryInfo(fx.DownloadsDirectory).Attributes.HasFlag(FileAttributes.ReparsePoint)
        );

        CreateSymbolicLinkUtility.Create(
            fx.DownloadsDirectory,
            fx.CacheDirectory,
            new PowerShellCmdBuilder()
        );

        var downloads = new DirectoryInfo(fx.DownloadsDirectory);
        Assert.True(downloads.Attributes.HasFlag(FileAttributes.ReparsePoint));
        Assert.Equal(fx.CacheDirectory, downloads.LinkTarget);
    }

    [Fact]
    public async Task RedownloadsWhenCachedArchiveMd5DoesNotMatch()
    {
        using var fx = AddonLayoutFixture.Create();
        var archive = fx.WriteArchive(fx.CacheDirectory);
        var record = fx.CreateGithubRecord("ffffffffffffffffffffffffffffffff");

        await record.DownloadAsync(TestContext.Current.CancellationToken);

        Assert.True(record.Downloaded);
        Assert.Equal(fx.ReplacementBytes, File.ReadAllBytes(record.DownloadPath));
        Assert.Equal(fx.ReplacementBytes, File.ReadAllBytes(archive.Path));
    }

    private static void AssertDidNotRedownload(
        GithubRecord record,
        AddonLayoutFixture fx,
        ArchiveFile archive
    )
    {
        Assert.False(record.Downloaded);
        Assert.Equal(0, fx.DownloadAttempts);
        Assert.Equal(archive.Bytes, File.ReadAllBytes(archive.Path));
        Assert.Contains(fx.ProgressTypes, type => type == GammaProgressType.CheckMd5);
        Assert.DoesNotContain(fx.ProgressTypes, type => type == GammaProgressType.Download);
    }

    private sealed class AddonLayoutFixture : IDisposable
    {
        public const string ArchiveName = "fixture-addon.zip";
        public byte[] ReplacementBytes { get; } = "downloaded-from-network"u8.ToArray();
        public List<GammaProgressType> ProgressTypes { get; } = [];
        public int DownloadAttempts { get; private set; }

        public string Root { get; }
        public string GammaDirectory { get; }
        public string CacheDirectory { get; }
        public string DownloadsDirectory { get; }

        private ServiceProvider? _services;

        private AddonLayoutFixture(
            string root,
            string gammaDirectory,
            string cacheDirectory,
            string downloadsDirectory
        )
        {
            Root = root;
            GammaDirectory = gammaDirectory;
            CacheDirectory = cacheDirectory;
            DownloadsDirectory = downloadsDirectory;
        }

        public static AddonLayoutFixture Create(bool symlinkDownloadsToCache = false)
        {
            var root = Path.Combine(Path.GetTempPath(), $"gamma-cache-reuse-{Guid.NewGuid():N}");
            var gammaDirectory = Path.Join(root, "gamma");
            var cacheDirectory = Path.Join(root, "cache");
            var downloadsDirectory = Path.Join(gammaDirectory, "downloads");
            Directory.CreateDirectory(gammaDirectory);
            Directory.CreateDirectory(cacheDirectory);
            if (symlinkDownloadsToCache)
            {
                Directory.CreateSymbolicLink(downloadsDirectory, cacheDirectory);
            }
            else
            {
                Directory.CreateDirectory(downloadsDirectory);
            }

            return new AddonLayoutFixture(root, gammaDirectory, cacheDirectory, downloadsDirectory);
        }

        public ArchiveFile WriteArchive(string directory)
        {
            Directory.CreateDirectory(directory);
            var bytes = "already-downloaded-addon"u8.ToArray();
            var path = Path.Join(directory, ArchiveName);
            File.WriteAllBytes(path, bytes);
            return new ArchiveFile(
                ArchiveName,
                path,
                bytes,
                Convert.ToHexStringLower(MD5.HashData(bytes))
            );
        }

        public GithubRecord CreateGithubRecord(string? md5)
        {
            var progress = new GammaProgress();
            progress.ProgressChanged += (_, args) => ProgressTypes.Add(args.ProgressType);

            var services = new ServiceCollection();
            services
                .AddHttpClient("dlAddon")
                .ConfigurePrimaryHttpMessageHandler(() =>
                    new CountingHandler(
                        () => DownloadAttempts++,
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new ByteArrayContent(ReplacementBytes),
                        }
                    )
                );
            _services = services.BuildServiceProvider();

            return new GithubRecord(
                progress,
                "Fixture Addon",
                "https://github.com/example/fixture-addon/archive/refs/heads/main.zip",
                "https://github.com/example/fixture-addon",
                ArchiveName,
                md5,
                GammaDirectory,
                "001- Fixture Addon",
                [],
                _services.GetRequiredService<IHttpClientFactory>(),
                new ArchiveService(null!, null!, null!),
                CacheDirectory
            );
        }

        public void Dispose()
        {
            _services?.Dispose();
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup of temp fixtures.
            }
        }
    }

    private sealed record ArchiveFile(string Name, string Path, byte[] Bytes, string Md5);

    private sealed class CountingHandler(Action onSend, HttpResponseMessage response)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            onSend();
            return Task.FromResult(response);
        }
    }
}
