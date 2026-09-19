using Stalker.Gamma.Factories;
using Xunit;

namespace Stalker.Gamma.Tests;

public sealed class ModPackMakerRecordFactoryTests
{
    [Fact]
    public void ParsesGitHubModPackMakerListSectionHeadersAndAddonRows()
    {
        const string githubListExcerpt = """
             Audio
            https://www.moddb.com/addons/start/222467	0	 - Grokitach	 Main Menu Theme - Deathcard Cabin	https://www.moddb.com/mods/stalker-anomaly/addons/groks-main-menu-theme-deathcard-cabin
             Visual
            https://www.moddb.com/addons/start/213337	0	 - Awene	 Agressor Reshade	https://www.moddb.com/mods/stalker-anomaly/addons/agressor-reshade
            """;

        var records = new ModPackMakerRecordFactory().Create(githubListExcerpt);

        Assert.Equal(4, records.Count);
        Assert.Equal("Audio", records[0].DlLink);
        Assert.True(string.IsNullOrWhiteSpace(records[0].AddonName));
        Assert.Equal("Main Menu Theme - Deathcard Cabin", records[1].AddonName);
        Assert.Equal("https://www.moddb.com/addons/start/222467", records[1].DlLink);
        Assert.Equal("Visual", records[2].DlLink);
        Assert.Equal("Agressor Reshade", records[3].AddonName);

        var separators = new SeparatorsFactory().Create(records);
        Assert.Equal(2, separators.Count);
        Assert.Equal("Audio Separator", separators[0].Name);
        Assert.Equal("Visual Separator", separators[1].Name);
    }
}
