using Stalker.Gamma.Models;

namespace Stalker.Gamma.GammaInstallerServices;

public interface IGetStalkerModsFromApi
{
    Task<string> GetModsAsync(CancellationToken cancellationToken);
}

public class GetStalkerModsFromApi(StalkerGammaSettings settings, IHttpClientFactory hcf)
    : IGetStalkerModsFromApi
{
    public async Task<string> GetModsAsync(CancellationToken cancellationToken) =>
        await _hc.GetStringAsync(settings.ModpackMakerList, cancellationToken);

    private readonly HttpClient _hc = hcf.CreateClient("stalkerApi");
}
