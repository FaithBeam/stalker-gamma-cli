using Stalker.Gamma.Models;

namespace Stalker.Gamma.GammaInstallerServices;

public interface IGetStalkerModsFromApi
{
    Task<string> GetModsAsync();
}

public class GetStalkerModsFromApi(StalkerGammaSettings settings, IHttpClientFactory hcf)
    : IGetStalkerModsFromApi
{
    public async Task<string> GetModsAsync() => await _hc.GetStringAsync(settings.ModpackMakerList);

    private readonly HttpClient _hc = hcf.CreateClient("stalkerApi");
}
