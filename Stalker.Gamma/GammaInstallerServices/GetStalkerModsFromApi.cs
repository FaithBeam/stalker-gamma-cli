using System.Net;
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
        catch (HttpRequestException e) when (ShouldFallback(modPackMakerListUrl, e))
        {
            try
            {
                return await _hc.GetStringAsync(ModPackMakerUrls.Default, cancellationToken);
            }
            catch (Exception fallbackException)
            {
                throw CreateException(fallbackException);
            }
        }
        catch (Exception e)
        {
            throw CreateException(e);
        }
    }

    private static bool ShouldFallback(string modPackMakerListUrl, HttpRequestException exception)
    {
        if (
            string.Equals(
                modPackMakerListUrl,
                ModPackMakerUrls.Default,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return false;
        }

        return exception.StatusCode
            is HttpStatusCode.NotFound
                or HttpStatusCode.InternalServerError;
    }

    private GetStalkerModsFromApiException CreateException(Exception exception) =>
        new(
            $"""
            Error getting mods from API
            ModPackMakerList: {settings.ModpackMakerList}
            Exception Message: {exception.Message}
            """,
            exception
        );

    private readonly HttpClient _hc = hcf.CreateClient("stalkerApi");
}

public class GetStalkerModsFromApiException(string msg, Exception exception)
    : Exception(msg, exception);
