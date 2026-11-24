namespace Stalker.Gamma.Tests;

/// <summary>
/// A unique folder under the system temp folder that's deleted when disposed
/// </summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; } =
        System.IO.Path.Join(
            System.IO.Path.GetTempPath(),
            "stalker-gamma-tests",
            Guid.NewGuid().ToString("N")
        );

    public TempDir() => Directory.CreateDirectory(Path);

    public string Join(params string[] segments) => System.IO.Path.Join([Path, .. segments]);

    /// <summary>
    /// Writes a file, creating its folders, and returns its path
    /// </summary>
    public string WriteFile(string relativePath, string contents = "")
    {
        var path = Join(relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
