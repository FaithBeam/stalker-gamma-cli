using Stalker.Gamma.Models;

namespace Stalker.Gamma.GammaInstallerServices.GammaInstaller;

public static class BrokenAddonRetry
{
    public static async Task RetryAsync(
        IReadOnlyList<IDownloadableRecord> brokenAddons,
        CancellationToken cancellationToken = default
    )
    {
        for (var i = 0; i < brokenAddons.Count; i++)
        {
            try
            {
                await brokenAddons[i].DownloadAsync(cancellationToken);
                await brokenAddons[i].ExtractAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                throw new RemainingAddonsDownloadException(
                    AppendRemaining(ex.Message, brokenAddons.Skip(i)),
                    ex
                );
            }
        }
    }

    private static string AppendRemaining(
        string errorMessage,
        IEnumerable<IDownloadableRecord> remainingAddons
    ) =>
        $"""
            {errorMessage}

            Remaining addons that still need to be downloaded:
            {string.Join(Environment.NewLine, remainingAddons.Select(FormatLine))}
            """;

    private static string FormatLine(IDownloadableRecord addon) =>
        string.IsNullOrWhiteSpace(addon.DownloadLink)
            ? $"- {addon.Name}"
            : $"- {addon.Name}: {addon.DownloadLink}";
}

public class RemainingAddonsDownloadException(string message, Exception innerException)
    : Exception(message, innerException);
