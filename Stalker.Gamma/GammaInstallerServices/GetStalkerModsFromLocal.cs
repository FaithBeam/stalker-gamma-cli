using System.Text.Json;
using Stalker.Gamma.Models;

namespace Stalker.Gamma.GammaInstallerServices;

public interface IGetStalkerModsFromLocal
{
    Task<List<ModPackMakerRecord>> GetMods(string gammaPath, string mo2Profile);
}

public class GetStalkerModsFromLocal : IGetStalkerModsFromLocal
{
    public async Task<List<ModPackMakerRecord>> GetMods(string gammaPath, string mo2Profile)
    {
        var pathToModPackMakerList = Path.Join(
            gammaPath,
            "profiles",
            mo2Profile,
            "modpack_maker_list.json"
        );
        if (!File.Exists(pathToModPackMakerList))
        {
            throw new GetStalkerModsFromLocalException(
                $"""
                Error reading modpack_maker_list.json at {pathToModPackMakerList}.
                File not found.
                You need to have ran command `full-install` at least once to be able to update.
                """
            );
        }

        return JsonSerializer.Deserialize<List<ModPackMakerRecord>>(
                await File.ReadAllTextAsync(pathToModPackMakerList),
                jsonTypeInfo: ModPackMakerCtx.Default.ListModPackMakerRecord
            ) ?? [];
    }
}

public class GetStalkerModsFromLocalException(string msg) : Exception(msg);
