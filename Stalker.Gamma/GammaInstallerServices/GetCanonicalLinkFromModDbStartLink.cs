using HtmlAgilityPack;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.GammaInstallerServices;

public class GetCanonicalLinkFromModDbStartLink(CurlUtility curlUtility)
{
    public async Task<string> GetCanonicalLinkAsync(
        string modDbStartLink,
        CancellationToken ct = default
    )
    {
        try
        {
            var htmlContent = await _curlUtility.GetStringAsync(modDbStartLink, ct);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);
            var linkNode = htmlDoc.DocumentNode.SelectSingleNode("//link[@rel='canonical']");
            var canonicalLink = linkNode?.GetAttributeValue("href", string.Empty);
            return string.IsNullOrWhiteSpace(canonicalLink)
                ? throw new CanonicalLinkNotFoundException(modDbStartLink)
                : canonicalLink;
        }
        catch (Exception e)
            when (e is not CanonicalLinkNotFoundException and not ModDbBotDetectedException)
        {
            throw new GetCanonicalLinkFromModDbStartLinkException(
                $"Error retrieving canonical link from: {modDbStartLink}",
                e
            );
        }
    }

    private readonly CurlUtility _curlUtility = curlUtility;
}

public class CanonicalLinkNotFoundException(string msg) : Exception(msg);

public class GetCanonicalLinkFromModDbStartLinkException(string msg, Exception inner)
    : Exception(msg, inner);
