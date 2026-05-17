using HtmlAgilityPack;
using Stalker.Gamma.Services;
using Stalker.Gamma.Utilities;
using CurlService = Stalker.Gamma.Services.CurlService;

namespace Stalker.Gamma.GammaInstallerServices;

public class GetCanonicalLinkFromModDbStartLink(CurlService curlService)
{
    public async Task<string> GetCanonicalLinkAsync(
        string modDbStartLink,
        CancellationToken ct = default
    )
    {
        string? htmlContent = null;
        try
        {
            htmlContent = await _curlService.GetStringAsync(modDbStartLink, ct);
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

    private readonly CurlService _curlService = curlService;
}

public class CanonicalLinkNotFoundException(string msg) : Exception(msg);

public class GetCanonicalLinkFromModDbStartLinkException(string msg, Exception inner)
    : Exception(msg, inner);
