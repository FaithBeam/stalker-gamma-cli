namespace stalker_gamma_cli.Utilities;

public class EnvChecker
{
    public static bool IsInPath(string exeName)
    {
        // 1. Get the PATH variable (handles OS differences)
        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVariable))
            return false;

        // 2. Split by the OS-specific path separator (; on Windows, : on Linux/macOS)
        var paths = pathVariable.Split(Path.PathSeparator);

        // 3. Check if the file exists in any of those directories
        return paths.Any(path =>
        {
            try
            {
                var fullPath = Path.Combine(path, exeName);
                return File.Exists(fullPath);
            }
            catch
            {
                // Ignore invalid paths in the PATH variable
                return false;
            }
        });
    }
}
