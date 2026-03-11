using System.Net.Http.Json;
using Stalker.Gamma.Models;

namespace Stalker.Gamma.GammaInstallerServices;

public interface IGetStalkerModsFromApi
{
    Task<ModsList> GetModsAsync(CancellationToken cancellationToken);
}

public class GetStalkerModsFromApi(StalkerGammaSettings settings, IHttpClientFactory hcf)
    : IGetStalkerModsFromApi
{
    public async Task<ModsList> GetModsAsync(CancellationToken cancellationToken) =>
        await _hc.GetFromJsonAsync(
            settings.ModpackMakerList,
            jsonTypeInfo: ModsListCtx.Default.ModsList,
            cancellationToken
        ) ?? throw new Exception("Failed to get mods from api");

    private readonly HttpClient _hc = hcf.CreateClient("stalkerApi");
}
