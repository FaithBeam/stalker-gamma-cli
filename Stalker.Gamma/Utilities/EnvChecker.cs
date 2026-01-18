namespace Stalker.Gamma.Utilities;

public class EnvChecker
{
    public static bool IsInPath(string exeName)
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVariable))
        {
            return false;
        }

        var paths = pathVariable.Split(Path.PathSeparator);

        return paths.Any(path =>
        {
            try
            {
                var fullPath = Path.Combine(path, exeName);
                return File.Exists(fullPath);
            }
            catch
            {
                return false;
            }
        });
    }
}
