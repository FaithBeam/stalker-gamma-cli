using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Stalker.Gamma.Utilities;

public partial class MirrorUtility(CurlUtility curlUtility)
{
    private static FrozenSet<string>? _availableMirrors;
    private static readonly SemaphoreSlim Lock = new(1);

    public async Task<string> GetMirrorAsync(
        string mirrorUrl,
        bool invalidateCache = false,
        CancellationToken cancellationToken = default,
        params string[] excludeMirrors
    )
    {
        await Lock.WaitAsync(cancellationToken);
        try
        {
            if (_availableMirrors is null || _availableMirrors.Count == 0 || invalidateCache)
            {
                _availableMirrors = await GetMirrorsAsync(mirrorUrl, cancellationToken);
            }

            var orderedAvailableMirrors = _availableMirrors
                .Where(mirror => excludeMirrors.All(em => !mirror.Contains(em)))
                .OrderBy(_ => Guid.NewGuid())
                .ToList();

            if (orderedAvailableMirrors.Count == 0)
            {
                throw new NoMirrorsAvailableException(
                    $"""
                    No mirrors available for {mirrorUrl}
                    This occurs when Moddb's servers are overloaded.
                    Try again later.
                    """
                );
            }

            return orderedAvailableMirrors.First();
        }
        catch (Exception e)
        {
            throw new MirrorUtilityException(
                $"""
                Error getting mirror
                Mirror URL: {mirrorUrl}
                """,
                e
            );
        }
        finally
        {
            Lock.Release();
        }
    }

    private async Task<FrozenSet<string>> GetMirrorsAsync(
        string mirrorUrl,
        CancellationToken cancellationToken = default
    )
    {
        var mirrorsHtml = await curlUtility.GetStringAsync(mirrorUrl, cancellationToken);
        if (mirrorsHtml.Contains("Just a moment..."))
        {
            throw new CloudflareChallengeException(
                $"""
                Cloudflare challenge detected.
                Mirror URL: {mirrorUrl}
                Mirrors HTML:
                {mirrorsHtml}
                """
            );
        }
        var matches = AvailableMirrors().Matches(mirrorsHtml);
        var matchSet = matches
            .Select(m =>
                m.Groups["href"]
                    .Value.Split(
                        '/',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                    )[3]
            )
            .ToFrozenSet();
        if (matchSet.Count == 0)
        {
            throw new MirrorUtilityException(
                $"""
                No mirrors found for {mirrorUrl}
                Mirrors HTML:
                {mirrorsHtml}
                """
            );
        }
        return matchSet;
    }

    [GeneratedRegex("""<a href="(?<href>.+)" id="downloadon">*?""")]
    private static partial Regex AvailableMirrors();
}

public class NoMirrorsAvailableException(string msg) : Exception(msg);

public class CloudflareChallengeException(string msg) : Exception(msg);

public class MirrorUtilityException : Exception
{
    public MirrorUtilityException(string msg)
        : base(msg) { }

    public MirrorUtilityException(string msg, Exception innerException)
        : base(msg, innerException) { }
}
