namespace Stalker.Gamma.Tests;

/// <summary>
/// Lines in the format of the mod pack maker list: link, instructions, patch, addon name,
/// ModDB page, archive name and MD5, separated by tabs. Taken from the real list.
/// </summary>
public static class ModPackMakerLines
{
    public static string Line(params string[] columns) => string.Join('\t', columns);

    public const string AudioSeparator = " Audio";

    public static readonly string MainMenuTheme = Line(
        "https://www.moddb.com/addons/start/222467",
        "0",
        " - Grokitach",
        " Main Menu Theme - Deathcard Cabin",
        "https://www.moddb.com/mods/stalker-anomaly/addons/groks-main-menu-theme-deathcard-cabin",
        "Groks_main_menu_theme.zip",
        "0f4256c9545fbbeda475f82796274d98"
    );

    public static readonly string StashOverhaul = Line(
        "https://www.moddb.com/addons/start/200140",
        "00. Grok's Stash Overhaul",
        " - Grokitach",
        " Stash Overhaul",
        "https://www.moddb.com/mods/stalker-anomaly/addons/groks-stash-overhaul-redux",
        "2.2.0_Groks_Stash_Overhaul.1.zip",
        "02ce6eb0546093445d88b4248d93df91"
    );

    public static readonly string StashOverhaulTbFix = Line(
        "https://www.moddb.com/addons/start/200140",
        "01. TB Fix Bugged Stashes Patch",
        " - Grokitach",
        " Stash Overhaul TBFix",
        "https://www.moddb.com/mods/stalker-anomaly/addons/groks-stash-overhaul-redux",
        "2.2.0_Groks_Stash_Overhaul.1.zip",
        "02ce6eb0546093445d88b4248d93df91"
    );

    /// <summary>
    /// A ModDB addon without an archive name, which is looked up on ModDB while downloading
    /// </summary>
    public static readonly string EftAimRattleMetadata = Line(
        "https://www.moddb.com/addons/start/209799",
        "0",
        " - Qudix",
        " EFT Aim Rattle (metadata)",
        "https://www.moddb.com/mods/stalker-anomaly/addons/eft-aim-rattle",
        "",
        ""
    );

    public static readonly string PoweredExos = Line(
        "https://github.com/ahuyn/anomaly-exo/archive/refs/tags/latest.zip",
        "anomaly-exo-latest",
        " - arti",
        " Powered Exos"
    );

    public static readonly string PoweredExosServoPatch = Line(
        "https://github.com/ahuyn/anomaly-exo/archive/refs/tags/latest.zip",
        @"anomaly-exo-latest\exo servo patch",
        " - arti",
        " Powered Exos ExoServomotorSounds patch"
    );

    /// <summary>
    /// The same repo as <see cref="PoweredExos"/> from another link, so its archive has the
    /// same name
    /// </summary>
    public static readonly string PoweredExosMaster = Line(
        "https://github.com/ahuyn/anomaly-exo/archive/refs/heads/master.zip",
        "anomaly-exo-master",
        " - arti",
        " Powered Exos master",
        "https://github.com/ahuyn/anomaly-exo",
        "",
        ""
    );

    public static readonly string MagsRedux = Line(
        "https://github.com/RAX-Anomaly/MagsRedux/archive/refs/heads/main.zip",
        @"\MagsRedux-main:\MagsRedux-main\aug_patch:\MagsRedux-main\bas_patch",
        " - RavenAscendant",
        " Anomaly Magazines",
        "https://www.moddb.com/mods/stalker-anomaly/addons/armr-arti-and-ravenascendants-mags-redux"
    );
}
