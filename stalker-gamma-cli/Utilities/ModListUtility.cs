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
            .Where(x => !string.IsNullOrWhiteSpace(x) && x[0] != '#')
            .Select(x => new ModListRecord
            {
                Status = x[0] switch
                {
                    '-' => ModStatus.Disabled,
                    '*' => ModStatus.Unmanaged,
                    _ => ModStatus.Enabled,
                },
                Name = x[1..],
            })
            .ToList();
    }

    public static async Task SaveModListAsync(string pathToModList, List<ModListRecord> mods) =>
        await File.WriteAllLinesAsync(
            pathToModList,
            mods.Select(x =>
                $"{x.Status switch
                {
                    ModStatus.Disabled => '-',
                    ModStatus.Unmanaged => '*',
                    _ => '+',
                }}{x.Name}"
            )
        );
}

public enum ModStatus
{
    Enabled,
    Disabled,

    /// <summary>
    /// Not managed by MO2, e.g. DLC, written with a * prefix
    /// </summary>
    Unmanaged,
}

public class ModListRecord
{
    public required ModStatus Status { get; set; }
    public required string Name { get; set; }
}
