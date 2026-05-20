using Stalker.Gamma.Models;

namespace Stalker.Gamma.GammaInstallerServices;

public interface IGetStalkerModsFromApi
{
    Task<string> GetModsAsync(CancellationToken cancellationToken);
    Task<string> GetModsAsync(string modPackMakerListUrl, CancellationToken cancellationToken);
}

public class GetStalkerModsFromApi(StalkerGammaSettings settings, IHttpClientFactory hcf)
    : IGetStalkerModsFromApi
{
    public async Task<string> GetModsAsync(CancellationToken cancellationToken) =>
        await GetModsAsync(settings.ModpackMakerList, cancellationToken);

    public async Task<string> GetModsAsync(
        string modPackMakerListUrl,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await _hc.GetStringAsync(modPackMakerListUrl, cancellationToken);
        }
        catch (Exception e)
        {
            throw new GetStalkerModsFromApiException(
                $"""
                Error getting mods from API
                ModPackMakerList: {settings.ModpackMakerList}
                Exception Message: {e.Message}
                """,
                e
            );
        }
    }

    private readonly HttpClient _hc = hcf.CreateClient("stalkerApi");
}

public class GetStalkerModsFromApiException(string msg, Exception exception)
    : Exception(msg, exception);
