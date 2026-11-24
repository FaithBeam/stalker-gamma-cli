using System.Reflection;
using stalker_gamma_cli.Models;

namespace stalker_gamma_cli.Tests;

/// <summary>
/// config set changes a profile with <see cref="CliProfile.TrySet"/>
/// </summary>
public class CliProfileTests
{
    [Theory]
    [InlineData("anomaly")]
    [InlineData("gamma")]
    [InlineData("cache")]
    public void Folders_are_stored_as_full_paths(string setting)
    {
        var profile = new CliProfile();

        Assert.True(profile.TrySet(setting, Path.Join(".", "rel-folder"), out var error));

        Assert.Null(error);
        var stored = setting switch
        {
            "anomaly" => profile.Anomaly,
            "gamma" => profile.Gamma,
            _ => profile.Cache,
        };
        // relative to where config set ran, not where a later command runs
        Assert.True(Path.IsPathFullyQualified(stored));
        Assert.Equal(Path.GetFullPath("rel-folder"), stored);
    }

    [Fact]
    public void Setting_names_ignore_case()
    {
        var profile = new CliProfile();

        Assert.True(profile.TrySet("StalkerGammaRepoBranch", "dev2", out _));
        Assert.True(
            profile.TrySet("GAMMASETUPREPOURL", "https://github.com/someone/gamma_setup", out _)
        );

        Assert.Equal("dev2", profile.StalkerGammaRepoBranch);
        Assert.Equal("https://github.com/someone/gamma_setup", profile.GammaSetupRepoUrl);
    }

    [Theory]
    [InlineData("profileName", "stable")]
    [InlineData("mo2Profile", "My Profile")]
    [InlineData("modPackMakerUrl", "http://127.0.0.1:8765/list.txt")]
    [InlineData("modListUrl", "http://127.0.0.1:8765/modlist.txt")]
    [InlineData("gammaSetupRepoBranch", "dev")]
    [InlineData("stalkerGammaRepoUrl", "https://github.com/someone/Stalker_GAMMA")]
    [InlineData("gammaLargeFilesRepoUrl", "https://github.com/someone/gamma_large_files_v2")]
    [InlineData("gammaLargeFilesRepoBranch", "dev")]
    [InlineData("teivazAnomalyGunslingerRepoUrl", "https://github.com/someone/teivaz")]
    [InlineData("teivazAnomalyGunslingerRepoBranch", "dev")]
    [InlineData("pythonApiUrl", "http://localhost:9000")]
    public void Text_settings_are_stored_as_given(string setting, string value)
    {
        var profile = new CliProfile();

        Assert.True(profile.TrySet(setting, value, out var error));

        Assert.Null(error);
        // each setting is named after the property it sets
        var property = typeof(CliProfile).GetProperty(
            setting,
            BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance
        );
        Assert.Equal(value, property!.GetValue(profile));
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("20", 20)]
    public void Download_threads_accepts_1_to_20(string value, int expected)
    {
        var profile = new CliProfile();

        Assert.True(profile.TrySet("downloadThreads", value, out _));

        Assert.Equal(expected, profile.DownloadThreads);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("21")]
    [InlineData("-1")]
    [InlineData("four")]
    [InlineData("")]
    public void Download_threads_rejects_other_values(string value)
    {
        var profile = new CliProfile();

        Assert.False(profile.TrySet("downloadThreads", value, out var error));

        Assert.Equal("Value must be an integer from 1 to 20.", error);
        Assert.Equal(2, profile.DownloadThreads);
    }

    [Fact]
    public void Unknown_setting_is_rejected()
    {
        var profile = new CliProfile();

        Assert.False(profile.TrySet("gammaFolder", "x", out var error));

        Assert.Equal("Unknown setting 'gammaFolder'.", error);
    }
}
