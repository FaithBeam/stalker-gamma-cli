using HtmlAgilityPack;
using Stalker.Gamma.Factories;
using Stalker.Gamma.ModDb.Services;
using Stalker.Gamma.Services;

namespace Stalker.Gamma.GammaInstallerServices;

public class GetCanonicalLinkFromModDbStartLink(NetworkServiceFactory networkServiceFactory)
{
    public async Task<string> GetCanonicalLinkAsync(
        string modDbStartLink,
        CancellationToken ct = default
    )
    {
        string? htmlContent = null;
        try
        {
            htmlContent = await networkServiceFactory
                .Create()
                .GetStringAsync(modDbStartLink, cancellationToken: ct);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);
            var linkNode = htmlDoc.DocumentNode.SelectSingleNode("//link[@rel='canonical']");
            var canonicalLink = linkNode?.GetAttributeValue("href", string.Empty);
            return string.IsNullOrWhiteSpace(canonicalLink)
                ? throw new CanonicalLinkNotFoundException(modDbStartLink)
                : canonicalLink;
        }
        catch (Exception e)
            when (e
                    is not CanonicalLinkNotFoundException
                        and not ModDbBotDetectedException
                        and not CloudflareChallengeException
            )
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
}

public class CanonicalLinkNotFoundException(string msg) : Exception(msg);

public class GetCanonicalLinkFromModDbStartLinkException(string msg, Exception inner)
    : Exception(msg, inner);
