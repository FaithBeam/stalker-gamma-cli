namespace Stalker.Gamma.ModOrganizer;

public static class WriteModOrganizerIni
{
    public static async Task WriteAsync(
        string gammaPath,
        string anomalyPath,
        string mo2Version,
        IList<string> separators,
        string selectedProfile = "G.A.M.M.A"
    )
    {
        var drivePrefix = OperatingSystem.IsWindows() ? "C:" : "Z:";
        var gammaPathWithDrivePrefix = (
            OperatingSystem.IsWindows() && Path.IsPathRooted(gammaPath)
                ? gammaPath
                : Path.Join(drivePrefix, Path.GetFullPath(gammaPath))
        ).Replace(@"\", "/");
        var anomalyPathWithDrivePrefix = (
            OperatingSystem.IsWindows() && Path.IsPathRooted(anomalyPath)
                ? anomalyPath
                : Path.Join(drivePrefix, Path.GetFullPath(anomalyPath))
        ).Replace(@"\", "/");
        var escapedWinAnomalyPath = OperatingSystem.IsWindows()
            ? anomalyPathWithDrivePrefix.Replace(@"\", @"\\")
            : anomalyPathWithDrivePrefix.TrimEnd('/').Replace("/", @"\\");
        var modOrganizerIniPath = Path.Join(gammaPath, "ModOrganizer.ini");
        await File.WriteAllTextAsync(
            modOrganizerIniPath,
            $"""
            [General]
            gameName=STALKER Anomaly
            selected_profile=@ByteArray({selectedProfile})
            gamePath=@ByteArray({escapedWinAnomalyPath})
            version={mo2Version.TrimStart('v')}
            first_start=false

            [PluginPersistance]
            Python%20Proxy\tryInit=false

            [customExecutables]
            size=10
            1\arguments=
            1\binary={anomalyPathWithDrivePrefix}/AnomalyLauncher.exe
            1\hide=false
            1\ownicon=true
            1\steamAppID=
            1\title=Anomaly Launcher
            1\toolbar=false
            1\workingDirectory={anomalyPathWithDrivePrefix}
            2\arguments=
            2\binary={anomalyPathWithDrivePrefix}/bin/AnomalyDX11AVX.exe
            2\hide=false
            2\ownicon=true
            2\steamAppID=
            2\title=Anomaly (DX11-AVX)
            2\toolbar=false
            2\workingDirectory={anomalyPathWithDrivePrefix}/bin
            3\arguments=
            3\binary={anomalyPathWithDrivePrefix}/bin/AnomalyDX11.exe
            3\hide=false
            3\ownicon=true
            3\steamAppID=
            3\title=Anomaly (DX11)
            3\toolbar=false
            3\workingDirectory={anomalyPathWithDrivePrefix}/bin
            4\arguments=
            4\binary={anomalyPathWithDrivePrefix}/bin/AnomalyDX10AVX.exe
            4\hide=false
            4\ownicon=true
            4\steamAppID=
            4\title=Anomaly (DX10-AVX)
            4\toolbar=false
            4\workingDirectory={anomalyPathWithDrivePrefix}/bin
            5\arguments=
            5\binary={anomalyPathWithDrivePrefix}/bin/AnomalyDX10.exe
            5\hide=false
            5\ownicon=true
            5\steamAppID=
            5\title=Anomaly (DX10)
            5\toolbar=false
            5\workingDirectory={anomalyPathWithDrivePrefix}/bin
            6\arguments=
            6\binary={anomalyPathWithDrivePrefix}/bin/AnomalyDX9AVX.exe
            6\hide=false
            6\ownicon=true
            6\steamAppID=
            6\title=Anomaly (DX9-AVX)
            6\toolbar=false
            6\workingDirectory={anomalyPathWithDrivePrefix}/bin
            7\arguments=
            7\binary={anomalyPathWithDrivePrefix}/bin/AnomalyDX9.exe
            7\hide=false
            7\ownicon=true
            7\steamAppID=
            7\title=Anomaly (DX9)
            7\toolbar=false
            7\workingDirectory={anomalyPathWithDrivePrefix}/bin
            8\arguments=
            8\binary={anomalyPathWithDrivePrefix}/bin/AnomalyDX8AVX.exe
            8\hide=false
            8\ownicon=true
            8\steamAppID=
            8\title=Anomaly (DX8-AVX)
            8\toolbar=false
            8\workingDirectory={anomalyPathWithDrivePrefix}/bin
            9\arguments=
            9\binary={anomalyPathWithDrivePrefix}/bin/AnomalyDX8.exe
            9\hide=false
            9\ownicon=true
            9\steamAppID=
            9\title=Anomaly (DX8)
            9\toolbar=false
            9\workingDirectory={anomalyPathWithDrivePrefix}/bin
            10\arguments=\"{escapedWinAnomalyPath}\"
            10\binary={gammaPathWithDrivePrefix}/explorer++/Explorer++.exe
            10\hide=false
            10\ownicon=true
            10\steamAppID=
            10\title=Explore Virtual Folder
            10\toolbar=false
            10\workingDirectory={gammaPathWithDrivePrefix}/explorer++

            [recentDirectories]
            size=0

            [Geometry]
            MainWindow_state=@ByteArray(\0\0\0\xff\0\0\0\0\xfd\0\0\0\x1\0\0\0\x3\0\0\x5\x14\0\0\0\xdb\xfc\x1\0\0\0\x1\xfb\0\0\0\xe\0l\0o\0g\0\x44\0o\0\x63\0k\x1\0\0\0\0\0\0\x5\x14\0\0\0\x62\0\xff\xff\xff\0\0\x5\x14\0\0\x1\xe3\0\0\0\x4\0\0\0\x4\0\0\0\b\0\0\0\b\xfc\0\0\0\x1\0\0\0\x2\0\0\0\x1\0\0\0\xe\0t\0o\0o\0l\0\x42\0\x61\0r\x1\0\0\0\0\xff\xff\xff\xff\0\0\0\0\0\0\0\0)
            MainWindow_geometry=@ByteArray(\x1\xd9\xd0\xcb\0\x3\0\0\0\0\0\xcc\0\0\0m\0\0\x5\xd5\0\0\x3\xad\0\0\0\xc7\0\0\0\x93\0\0\x5\xda\0\0\x3\xb2\0\0\0\0\0\0\0\0\x6\xc0\0\0\0\xc7\0\0\0\x93\0\0\x5\xda\0\0\x3\xb2)
            MainWindow_docks_logDock_size=219
            MainWindow_menuBar_visibility=true
            MainWindow_statusBar_visibility=true
            MainWindow_toolBar_visibility=true
            toolbar_size=@Size(42 36)
            toolbar_button_style=0
            MainWindow_splitter_state=@ByteArray(\0\0\0\xff\0\0\0\x1\0\0\0\x2\0\0\x4M\0\0\x2\xb8\x1\xff\xff\xff\xff\x1\0\0\0\x1\0)
            MainWindow_categoriesSplitter_state=@ByteArray(\0\0\0\xff\0\0\0\x1\0\0\0\x2\0\0\x1\b\0\0\x2\xcb\0\xff\xff\xff\xff\x1\0\0\0\x1\0)
            MainWindow_monitor=0
            MainWindow_categoriesGroup_visibility=false
            MainWindow_espList_state=@ByteArray(\0\0\0\xff\0\0\0\0\0\0\0\x1\0\0\0\0\0\0\0\x2\x1\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\x2h\0\0\0\x4\x1\x1\0\x1\0\0\0\0\0\0\0\0\x1\0\0\0\x82\xff\xff\xff\xff\0\0\0\x81\0\0\0\0\0\0\0\x4\0\0\x1\xa4\0\0\0\x1\0\0\0\0\0\0\0.\0\0\0\x1\0\0\0\0\0\0\0?\0\0\0\x1\0\0\0\0\0\0\0W\0\0\0\x1\0\0\0\0\0\0\x3\xe8\0\0\0\0W)
            MainWindow_downloadView_state=@ByteArray(\0\0\0\xff\0\0\0\0\0\0\0\x1\0\0\0\x1\0\0\0\x1\x1\0\0\0\0\0\0\0\0\0\0\0\b\xf0\0\0\0\x4\0\0\0\x4\0\0\0\x64\0\0\0\x5\0\0\0\x64\0\0\0\x6\0\0\0\x64\0\0\0\a\0\0\0\x64\0\0\x2R\0\0\0\b\x1\x1\0\0\x1\0\0\0\0\0\0\0\0\0\0\0\x64\xff\xff\xff\xff\0\0\0\x81\0\0\0\0\0\0\0\b\0\0\x1<\0\0\0\x1\0\0\0\0\0\0\0N\0\0\0\x1\0\0\0\0\0\0\0\x64\0\0\0\x1\0\0\0\0\0\0\0\x64\0\0\0\x1\0\0\0\0\0\0\0\0\0\0\0\x1\0\0\0\0\0\0\0\0\0\0\0\x1\0\0\0\0\0\0\0\0\0\0\0\x1\0\0\0\0\0\0\0\0\0\0\0\x1\0\0\0\0\0\0\x3\xe8\x1\0\0\0\0)
            MainWindow_savegameList_state=@ByteArray(\0\0\0\xff\0\0\0\0\0\0\0\x1\0\0\0\x1\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\x2h\0\0\0\x2\x1\0\0\x1\0\0\0\0\0\0\0\0\0\0\0\0\x82\xff\xff\xff\xff\0\0\0\x81\0\0\0\0\0\0\0\x2\0\0\0\x82\0\0\0\x1\0\0\0\0\0\0\x1\xe6\0\0\0\x1\0\0\0\0\0\0\x3\xe8\0\0\0\0\x82)
            MainWindow_dataTree_state=@ByteArray(\0\0\0\xff\0\0\0\0\0\0\0\x1\0\0\0\0\0\0\0\0\x1\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\x2\xd0\0\0\0\x5\x1\x1\0\x1\0\0\0\0\0\0\0\0\0\0\0\0\x82\xff\xff\xff\xff\0\0\0\x81\0\0\0\0\0\0\0\x5\0\0\0\xc8\0\0\0\x1\0\0\0\0\0\0\0\x82\0\0\0\x1\0\0\0\0\0\0\0\x82\0\0\0\x1\0\0\0\0\0\0\0\x82\0\0\0\x1\0\0\0\0\0\0\0\x82\0\0\0\x1\0\0\0\0\0\0\x3\xe8\0\0\0\0\x82)
            MainWindow_modList_state=@ByteArray(\0\0\0\xff\0\0\0\0\0\0\0\x1\0\0\0\0\0\0\0\t\x1\0\0\0\0\0\0\0\0\0\0\0\vh\x5\0\0\0\x5\0\0\0\x3\0\0\0(\0\0\0\x5\0\0\0(\0\0\0\x6\0\0\0(\0\0\0\b\0\0\0(\0\0\0\n\0\0\0(\0\0\x5*\0\0\0\v\x1\x1\0\x1\0\0\0\0\0\0\0\0\x1\0\0\0(\xff\xff\xff\xff\0\0\0\x81\0\0\0\0\0\0\0\v\0\0\x1\x85\0\0\0\x1\0\0\0\0\0\0\0\x46\0\0\0\x1\0\0\0\0\0\0\0.\0\0\0\x1\0\0\0\0\0\0\0\0\0\0\0\x1\0\0\0\0\0\0\0Y\0\0\0\x1\0\0\0\0\0\0\0\0\0\0\0\x1\0\0\0\0\0\0\0\0\0\0\0\x1\0\0\0\0\0\0\x2\x99\0\0\0\x1\0\0\0\0\0\0\0\0\0\0\0\x1\0\0\0\0\0\0\0?\0\0\0\x1\0\0\0\0\0\0\0\0\0\0\0\x1\0\0\0\0\0\0\x3\xe8\x1\0\0\0?)

            [Widgets]
            MainWindow_executablesListBox_index=1
            MainWindow_tabWidget_index=0
            MainWindow_dataTabShowOnlyConflicts_checked=false
            MainWindow_dataTabShowFromArchives_checked=false
            MainWindow_groupCombo_index=0
            MainWindow_modList_index={string.Join(
                ", ",
                separators
            )}
            MainWindow_filters_index=0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            MainWindow_filtersAnd_checked=true
            MainWindow_filtersOr_checked=false
            MainWindow_filtersSeparators_index=0

            [Settings]
            filter_regex=false
            regex_case_sensitive=false
            regex_extended=false
            filter_scroll_to_selection=false

            """
        );
    }
}
