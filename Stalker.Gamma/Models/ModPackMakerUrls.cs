namespace Stalker.Gamma.Models;

public static class ModPackMakerUrls
{
    public const string Default =
        "https://raw.githubusercontent.com/Grokitach/Stalker_GAMMA/refs/heads/main/G.A.M.M.A/modpack_data/modpack_maker_list.txt";

    public static bool IsObsoleteOfficialListUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.Host.Equals("stalker-gamma.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        return path.Equals("/api/client/v1/mods/list", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/list", StringComparison.OrdinalIgnoreCase);
    }
}
