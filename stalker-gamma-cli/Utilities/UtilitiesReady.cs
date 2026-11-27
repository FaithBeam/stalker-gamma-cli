using LibSevenZip;

namespace stalker_gamma_cli.Utilities;

/// <summary>
/// Checks the native 7-Zip library loads. libcurl-impersonate is only loaded when a ModDB request
/// goes through curl, so a missing one fails that request with the reason instead.
/// </summary>
public class UtilitiesReady
{
    public bool IsReady => SevenZipError is null;

    public string NotReadyReason => SevenZipError is { } error ? $"7z: {error}" : "";

    private static string? SevenZipError
    {
        get
        {
            try
            {
                SevenZipLibrary.Load();
                return null;
            }
            catch (Exception e)
            {
                return $"Not Ready ({e.Message})";
            }
        }
    }
}
