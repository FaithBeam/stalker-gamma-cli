namespace Stalker.Gamma.Models;

public interface ISeparator
{
    string Name { get; init; }
    string FolderName { get; init; }
    Task WriteAsync(string gammaPath);
}

public class Separator : ISeparator
{
    public required string Name { get; init; }
    public required string FolderName { get; init; }

    public async Task WriteAsync(string gammaPath)
    {
        var separatorPath = Path.Join(gammaPath, "mods", FolderName);
        Directory.CreateDirectory(separatorPath);
        await File.WriteAllTextAsync(
            Path.Join(separatorPath, "meta.ini"),
            SeparatorMetaIni.ReplaceLineEndings("\r\n")
        );
    }

    private const string SeparatorMetaIni = """
        [General]
        modid=0
        version=
        newestVersion=
        category=0
        installationFile=

        [installedFiles]
        size=0

        """;
}
