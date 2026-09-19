using System.Text.Json;
using stalker_gamma_cli.Commands;
using stalker_gamma_cli.Models;
using Stalker.Gamma.Models;
using Xunit;

namespace Stalker.Gamma.Tests;

public sealed class ModPackMakerUrlTests
{
    private const string GitHubModPackMakerList =
        "https://raw.githubusercontent.com/Grokitach/Stalker_GAMMA/refs/heads/main/G.A.M.M.A/modpack_data/modpack_maker_list.txt";

    [Fact]
    public void NewDefaultsUseGitHubRawModPackMakerList()
    {
        Assert.Equal(GitHubModPackMakerList, new CliProfile().ModPackMakerUrl);
        Assert.Equal(GitHubModPackMakerList, new StalkerGammaSettings().ModpackMakerList);

        var defaultUrl = typeof(Config)
            .GetMethod(nameof(Config.Create))!
            .GetParameters()
            .Single(parameter => parameter.Name == "modPackMakerUrl")
            .DefaultValue;

        Assert.Equal(GitHubModPackMakerList, defaultUrl);
    }

    [Fact]
    public void MigratesBakedInDeadStalkerGammaApiUrlsAndLeavesCustomUrlsAlone()
    {
        const string customUrl = "https://example.com/custom-modpack-maker-list.txt";
        var settings = JsonSerializer.Deserialize(
            """
            {
              "Profiles": [
                {
                  "ProfileName": "v1",
                  "ModPackMakerUrl": "https://stalker-gamma.com/api/client/v1/mods/list",
                  "Active": true
                },
                {
                  "ProfileName": "legacy",
                  "ModPackMakerUrl": "https://stalker-gamma.com/api/list"
                },
                {
                  "ProfileName": "custom",
                  "ModPackMakerUrl": "https://example.com/custom-modpack-maker-list.txt"
                }
              ]
            }
            """,
            CliSettingsCtx.Default.CliSettings
        )!;

        var changed = settings.MigrateObsoleteModPackMakerUrls();

        Assert.True(changed);
        Assert.Equal(GitHubModPackMakerList, settings.Profiles[0].ModPackMakerUrl);
        Assert.Equal(GitHubModPackMakerList, settings.Profiles[1].ModPackMakerUrl);
        Assert.Equal(customUrl, settings.Profiles[2].ModPackMakerUrl);
    }

    [Fact]
    public void DoesNotRewriteCustomOrCurrentModPackMakerUrls()
    {
        var settings = new CliSettings
        {
            Profiles =
            [
                new CliProfile { ModPackMakerUrl = GitHubModPackMakerList },
                new CliProfile
                {
                    ModPackMakerUrl = "https://example.com/custom-modpack-maker-list.txt",
                },
            ],
        };

        Assert.False(settings.MigrateObsoleteModPackMakerUrls());
        Assert.Equal(GitHubModPackMakerList, settings.Profiles[0].ModPackMakerUrl);
        Assert.Equal(
            "https://example.com/custom-modpack-maker-list.txt",
            settings.Profiles[1].ModPackMakerUrl
        );
    }
}
