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
                $"{pathToModPackMakerList} file not found, please run `full-install` and let it complete to generate this file. You can then perform `update check` and `update apply`"
            );
        }

        return JsonSerializer.Deserialize<List<ModPackMakerRecord>>(
                await File.ReadAllTextAsync(pathToModPackMakerList),
                jsonTypeInfo: ModPackMakerCtx.Default.ListModPackMakerRecord
            ) ?? [];
    }
}

public class GetStalkerModsFromLocalException(string msg) : Exception(msg);
