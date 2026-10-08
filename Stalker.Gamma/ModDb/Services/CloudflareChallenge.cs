namespace Stalker.Gamma.ModDb.Services;

public static class CloudflareChallenge
{
    public static string ThrowIfChallenged(string html, string url) =>
        html.Contains("Just a moment...")
            ? throw new CloudflareChallengeException(
                $"""
                Cloudflare challenge detected when retrieving: {url}
                Use a VPN to another country or visit https://github.com/FaithBeam/stalker-gamma-cli/wiki/Stalker-GAMMA-Server
                """
            )
            : html;
}

public class CloudflareChallengeException(string msg) : Exception(msg);
