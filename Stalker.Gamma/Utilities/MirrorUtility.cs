using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Stalker.Gamma.Utilities;

public partial class MirrorUtility(CurlUtility curlUtility)
{
    private static FrozenSet<string>? _mirrors;
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
            _mirrors =
                _mirrors is null || _mirrors.Count == 0 || invalidateCache
                    ? await GetMirrorsAsync(mirrorUrl, cancellationToken)
                    : _mirrors;
        }
        finally
        {
            Lock.Release();
        }
        return _mirrors
            .Where(mirror => excludeMirrors.All(em => !mirror.Contains(em)))
            .OrderBy(_ => Guid.NewGuid())
            .First();
    }

    private async Task<FrozenSet<string>> GetMirrorsAsync(
        string mirrorUrl,
        CancellationToken cancellationToken = default
    )
    {
        var mirrorsHtml = await curlUtility.GetStringAsync(mirrorUrl, cancellationToken);
        var matches = HrefRx().Matches(mirrorsHtml);
        return matches
            .Select(m =>
                m.Groups["href"]
                    .Value.Split(
                        '/',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                    )[3]
            )
            .ToFrozenSet();
    }

    [GeneratedRegex("""<a href="(?<href>.+)" id="downloadon">*?""")]
    private static partial Regex HrefRx();
}
