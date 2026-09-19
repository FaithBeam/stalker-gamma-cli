namespace Stalker.Gamma.Utilities;

internal static class ExecutableResolver
{
    public static string? Resolve(string configuredPath, string defaultCommand)
    {
        if (IsExecutableFile(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        if (IsBareCommand(configuredPath) && FindOnPath(configuredPath) is { } configuredOnPath)
        {
            return configuredOnPath;
        }

        return FindOnPath(defaultCommand);
    }

    private static bool IsBareCommand(string command) =>
        command.IndexOfAny(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, Path.VolumeSeparatorChar]
        ) < 0;

    private static string? FindOnPath(string command)
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVariable))
        {
            return null;
        }

        foreach (var directory in pathVariable.Split(Path.PathSeparator))
        {
            foreach (var commandName in GetCommandNames(command))
            {
                try
                {
                    var candidate = Path.Combine(directory, commandName);
                    if (IsExecutableFile(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }
                catch
                {
                    // Ignore malformed PATH entries and continue searching.
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> GetCommandNames(string command)
    {
        yield return command;

        if (OperatingSystem.IsWindows() && !Path.HasExtension(command))
        {
            yield return $"{command}.exe";
        }
    }

    private static bool IsExecutableFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            if (OperatingSystem.IsWindows())
            {
                return true;
            }

            const UnixFileMode executePermissions =
                UnixFileMode.UserExecute
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherExecute;
            return (File.GetUnixFileMode(path) & executePermissions) != 0;
        }
        catch
        {
            return false;
        }
    }
}
