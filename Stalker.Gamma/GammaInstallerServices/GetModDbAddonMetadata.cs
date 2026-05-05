using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Stalker.Gamma.Models;
using Stalker.Gamma.Proxies;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.GammaInstallerServices;

public partial class GetModDbAddonMetadata(PythonApiProxy pythonApiProxy)
{
    private readonly PythonApiProxy _pythonApiProxy = pythonApiProxy;

    public async Task<ModDbPageMetadata> GetAsync(string modDbAddonUrl, CancellationToken ct)
    {
        var addonHtml = await _pythonApiProxy.GetStringAsync(modDbAddonUrl, ct);
        try
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(addonHtml);
            var node = htmlDoc.DocumentNode.SelectNodes(
                "//div[contains(@class, 'table') and contains(@class, 'tablemenu')]//div[contains(@class, 'row') and contains(@class, 'clear')]"
            );
            var modDbAddonMetadataDict = node.Select(n => new
                {
                    Title = n.ChildNodes.FirstOrDefault(cn => cn.Name == "h5")?.InnerText.Trim(),
                    Value = n.ChildNodes.FirstOrDefault(cn => cn.Name == "span")?.InnerText.Trim(),
                })
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.Title)
                    && Wanted.Contains(x.Title)
                    && !string.IsNullOrWhiteSpace(x.Value)
                )
                .ToDictionary(x => x.Title!, x => x.Value!);
            modDbAddonMetadataDict.TryGetValue("Credits", out var credits);
            var modDbPageMetadata = new ModDbPageMetadata
            {
                Url = modDbAddonUrl,
                Added = DateTimeOffset.ParseExact(
                    CleanDateRx().Replace(modDbAddonMetadataDict["Added"], ""),
                    "MMM d, yyyy",
                    CultureInfo.InvariantCulture
                ),
                Category = modDbAddonMetadataDict["Category"],
                Credits = credits,
                Downloads = long.Parse(
                    modDbAddonMetadataDict["Downloads"].Split(' ')[0],
                    NumberStyles.AllowThousands,
                    provider: CultureInfo.InvariantCulture
                ),
                Filename = modDbAddonMetadataDict["Filename"],
                Md5Hash = modDbAddonMetadataDict["MD5 Hash"],
                Size = long.Parse(
                    SizeRx().Match(modDbAddonMetadataDict["Size"]).Groups[1].Value,
                    NumberStyles.AllowThousands,
                    provider: CultureInfo.InvariantCulture
                ),
                Updated = modDbAddonMetadataDict.TryGetValue("Updated", out var updated)
                    ? DateTimeOffset.ParseExact(
                        CleanDateRx().Replace(updated, ""),
                        "MMM d, yyyy",
                        CultureInfo.InvariantCulture
                    )
                    : DateTimeOffset.ParseExact(
                        CleanDateRx().Replace(modDbAddonMetadataDict["Added"], ""),
                        "MMM d, yyyy",
                        CultureInfo.InvariantCulture
                    ),
                Uploader = modDbAddonMetadataDict["Uploader"],
            };
            return modDbPageMetadata;
        }
        catch (Exception e)
        {
            throw new GetModDbAddonMetadataException(
                $"""
                Error getting metadata for {modDbAddonUrl}
                Addon HTML: {addonHtml}
                Exception message: {e.Message}
                """,
                e
            );
        }
    }

    [GeneratedRegex(@"(?<=\d)(st|nd|rd|th)")]
    private partial Regex CleanDateRx();

    [GeneratedRegex(@".*\((.*) bytes\)")]
    private partial Regex SizeRx();

    private static readonly HashSet<string> Wanted =
    [
        "Location",
        "Filename",
        "Category",
        "Licence",
        "Uploader",
        "Credits",
        "Added",
        "Size",
        "Downloads",
        "MD5 Hash",
    ];
}

public class GetModDbAddonMetadataException(string msg, Exception exception)
    : Exception(msg, exception);
