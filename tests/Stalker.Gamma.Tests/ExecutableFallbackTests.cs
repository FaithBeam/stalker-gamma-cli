using Stalker.Gamma.Models;
using Stalker.Gamma.Services;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Stalker.Gamma.Tests;

public sealed class ExecutableFallbackTests
{
    [Fact]
    public async Task SevenZipUsesDefaultCommandOnPathWhenConfiguredExecutableIsMissing()
    {
        using var fixture = new FakeExecutableFixture(DefaultCommand("7zz"));
        using var environment = new EnvironmentVariableScope(
            fixture.DirectoryPath,
            fixture.OutputPath
        );
        var settings = new StalkerGammaSettings
        {
            PathTo7Z = Path.Combine(fixture.DirectoryPath, "missing", DefaultCommand("7zz")),
        };
        var service = new SevenZipService(settings);

        Assert.True(service.Ready);

        await service.ExtractAsync(
            Path.Combine(fixture.DirectoryPath, "archive.7z"),
            Path.Combine(fixture.DirectoryPath, "extracted"),
            cancellationToken: TestContext.Current.CancellationToken
        );

        var invocation = await File.ReadAllLinesAsync(
            fixture.OutputPath,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(fixture.ExecutablePath, invocation[0]);
    }

    [Fact]
    public async Task TarUsesDefaultCommandOnPathWhenConfiguredExecutableIsMissing()
    {
        using var fixture = new FakeExecutableFixture(DefaultCommand("tar"));
        using var environment = new EnvironmentVariableScope(
            fixture.DirectoryPath,
            fixture.OutputPath
        );
        var settings = new StalkerGammaSettings
        {
            PathToTar = Path.Combine(fixture.DirectoryPath, "missing", DefaultCommand("tar")),
        };
        var service = new TarService(settings);

        Assert.True(service.Ready);

        await service.ExtractAsync(
            Path.Combine(fixture.DirectoryPath, "archive.tar.gz"),
            Path.Combine(fixture.DirectoryPath, "extracted"),
            onProgress: null,
            TestContext.Current.CancellationToken
        );

        var invocation = await File.ReadAllLinesAsync(
            fixture.OutputPath,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(fixture.ExecutablePath, invocation[0]);
    }

    [Fact]
    public async Task UnzipUsesDefaultCommandOnPathWhenConfiguredExecutableIsMissing()
    {
        using var fixture = new FakeExecutableFixture(DefaultCommand("unzip"));
        using var environment = new EnvironmentVariableScope(
            fixture.DirectoryPath,
            fixture.OutputPath
        );
        var settings = new StalkerGammaSettings
        {
            PathToUnzip = Path.Combine(
                fixture.DirectoryPath,
                "missing",
                DefaultCommand("unzip")
            ),
        };
        var service = new UnzipService(settings);

        Assert.True(service.Ready);

        await service.ExtractAsync(
            Path.Combine(fixture.DirectoryPath, "archive.zip"),
            Path.Combine(fixture.DirectoryPath, "extracted"),
            onProgress: null,
            TestContext.Current.CancellationToken
        );

        var invocation = await File.ReadAllLinesAsync(
            fixture.OutputPath,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(fixture.ExecutablePath, invocation[0]);
    }

    [Fact]
    public async Task SevenZipPrefersExistingConfiguredExecutableOverDefaultCommandOnPath()
    {
        using var configuredFixture = new FakeExecutableFixture(DefaultCommand("bundled-7zz"));
        using var pathFixture = new FakeExecutableFixture(DefaultCommand("7zz"));
        using var environment = new EnvironmentVariableScope(
            pathFixture.DirectoryPath,
            configuredFixture.OutputPath
        );
        var settings = new StalkerGammaSettings
        {
            PathTo7Z = configuredFixture.ExecutablePath,
        };
        var service = new SevenZipService(settings);

        Assert.True(service.Ready);

        await service.ExtractAsync(
            Path.Combine(configuredFixture.DirectoryPath, "archive.7z"),
            Path.Combine(configuredFixture.DirectoryPath, "extracted"),
            cancellationToken: TestContext.Current.CancellationToken
        );

        var invocation = await File.ReadAllLinesAsync(
            configuredFixture.OutputPath,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(configuredFixture.ExecutablePath, invocation[0]);
    }

    [Fact]
    public async Task SevenZipPrefersConfiguredBareCommandOverDefaultCommandOnPath()
    {
        using var defaultFixture = new FakeExecutableFixture(DefaultCommand("7zz"));
        using var configuredFixture = new FakeExecutableFixture(DefaultCommand("custom-7zz"));
        using var environment = new EnvironmentVariableScope(
            string.Join(
                Path.PathSeparator,
                defaultFixture.DirectoryPath,
                configuredFixture.DirectoryPath
            ),
            configuredFixture.OutputPath
        );
        var settings = new StalkerGammaSettings
        {
            PathTo7Z = DefaultCommand("custom-7zz"),
        };
        var service = new SevenZipService(settings);

        Assert.True(service.Ready);

        await service.ExtractAsync(
            Path.Combine(configuredFixture.DirectoryPath, "archive.7z"),
            Path.Combine(configuredFixture.DirectoryPath, "extracted"),
            cancellationToken: TestContext.Current.CancellationToken
        );

        var invocation = await File.ReadAllLinesAsync(
            configuredFixture.OutputPath,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(configuredFixture.ExecutablePath, invocation[0]);
    }

    [Fact]
    public async Task SevenZipFindsConfiguredExtensionlessCommandOnPathOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows-only behavior.");
            return;
        }

        using var defaultFixture = new FakeExecutableFixture(DefaultCommand("7zz"));
        using var configuredFixture = new FakeExecutableFixture(DefaultCommand("custom-7z"));
        using var environment = new EnvironmentVariableScope(
            string.Join(
                Path.PathSeparator,
                defaultFixture.DirectoryPath,
                configuredFixture.DirectoryPath
            ),
            configuredFixture.OutputPath
        );
        var settings = new StalkerGammaSettings
        {
            PathTo7Z = "custom-7z",
        };
        var service = new SevenZipService(settings);

        Assert.True(service.Ready);

        await service.ExtractAsync(
            Path.Combine(configuredFixture.DirectoryPath, "archive.7z"),
            Path.Combine(configuredFixture.DirectoryPath, "extracted"),
            cancellationToken: TestContext.Current.CancellationToken
        );

        var invocation = await File.ReadAllLinesAsync(
            configuredFixture.OutputPath,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(configuredFixture.ExecutablePath, invocation[0]);
    }

    [Fact]
    public async Task SevenZipSkipsNonExecutableConfiguredAndPathCandidatesOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix-only behavior.");
            return;
        }

        using var configuredFixture = new FakeExecutableFixture("bundled-7zz");
        using var firstPathFixture = new FakeExecutableFixture("7zz");
        using var executableFixture = new FakeExecutableFixture("7zz");
        File.SetUnixFileMode(
            configuredFixture.ExecutablePath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite
        );
        File.SetUnixFileMode(
            firstPathFixture.ExecutablePath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite
        );
        using var environment = new EnvironmentVariableScope(
            string.Join(
                Path.PathSeparator,
                firstPathFixture.DirectoryPath,
                executableFixture.DirectoryPath
            ),
            executableFixture.OutputPath
        );
        var settings = new StalkerGammaSettings
        {
            PathTo7Z = configuredFixture.ExecutablePath,
        };
        var service = new SevenZipService(settings);

        Assert.True(service.Ready);

        await service.ExtractAsync(
            Path.Combine(executableFixture.DirectoryPath, "archive.7z"),
            Path.Combine(executableFixture.DirectoryPath, "extracted"),
            cancellationToken: TestContext.Current.CancellationToken
        );

        var invocation = await File.ReadAllLinesAsync(
            executableFixture.OutputPath,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(executableFixture.ExecutablePath, invocation[0]);
    }

    private static string DefaultCommand(string command) =>
        OperatingSystem.IsWindows() ? $"{command}.exe" : command;

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string? originalPath = Environment.GetEnvironmentVariable("PATH");
        private readonly string? originalOutput = Environment.GetEnvironmentVariable(
            "FAKE_EXECUTABLE_OUTPUT"
        );

        public EnvironmentVariableScope(string path, string outputPath)
        {
            Environment.SetEnvironmentVariable("PATH", path);
            Environment.SetEnvironmentVariable("FAKE_EXECUTABLE_OUTPUT", outputPath);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Environment.SetEnvironmentVariable("FAKE_EXECUTABLE_OUTPUT", originalOutput);
        }
    }

    private sealed class FakeExecutableFixture : IDisposable
    {
        public FakeExecutableFixture(string command)
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), $"stalker-gamma-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(DirectoryPath);

            var sourceExecutable = GetSourceExecutable();
            var sourceDirectory = Path.GetDirectoryName(sourceExecutable)!;
            foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory, "FakeExecutable*"))
            {
                File.Copy(sourceFile, Path.Combine(DirectoryPath, Path.GetFileName(sourceFile)));
            }

            ExecutablePath = Path.Combine(DirectoryPath, command);
            File.Copy(sourceExecutable, ExecutablePath);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(ExecutablePath, File.GetUnixFileMode(sourceExecutable));
            }

            OutputPath = Path.Combine(DirectoryPath, "invocation.txt");
        }

        public string DirectoryPath { get; }
        public string ExecutablePath { get; }
        public string OutputPath { get; }

        public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);

        private static string GetSourceExecutable()
        {
            var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            var executableName = DefaultCommand("FakeExecutable");
            return Path.GetFullPath(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "../../../..",
                    "FakeExecutable",
                    "bin",
                    configuration,
                    "net10.0",
                    executableName
                )
            );
        }
    }
}
