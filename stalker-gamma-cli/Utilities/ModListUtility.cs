namespace stalker_gamma_cli.Utilities;

public static class ModListUtility
{
    public static async Task<List<ModListRecord>> GetModListAsync(string pathToModList)
    {
        if (!File.Exists(pathToModList))
        {
            throw new FileNotFoundException($"File {pathToModList} doesn't exist");
        }

        return (await File.ReadAllLinesAsync(pathToModList))
            .Where(x => x[0] != '#')
            .Select(x =>
            {
                var active = x.StartsWith('-') ? ModStatus.Disabled : ModStatus.Enabled;
                return new ModListRecord { Status = active, Name = x[1..] };
            })
            .ToList();
    }

    public static async Task SaveModListAsync(string pathToModList, List<ModListRecord> mods) =>
        await File.WriteAllLinesAsync(
            pathToModList,
            mods.Select(x => $"{(x.Status == ModStatus.Enabled ? "+" : "-")}{x.Name}")
        );
}

public enum ModStatus
{
    Enabled,
    Disabled,
}

public class ModListRecord
{
    public required ModStatus Status { get; set; }
    public required string Name { get; set; }
}
