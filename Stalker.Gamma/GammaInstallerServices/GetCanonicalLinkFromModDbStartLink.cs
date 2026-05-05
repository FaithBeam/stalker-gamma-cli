using HtmlAgilityPack;
using Stalker.Gamma.Proxies;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.GammaInstallerServices;

public class GetCanonicalLinkFromModDbStartLink(PythonApiProxy pythonApiProxy)
{
    public async Task<string> GetCanonicalLinkAsync(
        string modDbStartLink,
        CancellationToken ct = default
    )
    {
        string? htmlContent = null;
        try
        {
            htmlContent = await _pythonApiProxy.GetStringAsync(modDbStartLink, ct);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);
            var linkNode = htmlDoc.DocumentNode.SelectSingleNode("//link[@rel='canonical']");
            var canonicalLink = linkNode.GetAttributeValue("href", string.Empty);
            return string.IsNullOrWhiteSpace(canonicalLink)
                ? throw new CanonicalLinkNotFoundException(modDbStartLink)
                : canonicalLink;
        }
        catch (Exception e)
            when (e is not CanonicalLinkNotFoundException and not ModDbBotDetectedException)
        {
            throw new GetCanonicalLinkFromModDbStartLinkException(
                $"""
                Error retrieving canonical link from
                ModDbStartLink: {modDbStartLink}
                Exception Message: {e.Message}
                HTML Content: {htmlContent}
                """,
                e
            );
        }
    }

    private readonly PythonApiProxy _pythonApiProxy = pythonApiProxy;
}

public class CanonicalLinkNotFoundException(string msg) : Exception(msg);

public class GetCanonicalLinkFromModDbStartLinkException(string msg, Exception inner)
    : Exception(msg, inner);
