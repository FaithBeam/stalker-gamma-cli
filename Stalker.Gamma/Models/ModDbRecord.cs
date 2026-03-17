using System.Security.Cryptography;
using Stalker.Gamma.GammaInstallerServices;
using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.Models;

public class ModDbRecord(
    GammaProgress gammaProgress,
    string name,
    string url,
    string niceUrl,
    string archiveName,
    string? md5,
    string gammaDir,
    string outputDirName,
    IList<string> instructions,
    ArchiveUtility archiveUtility,
    ModDbUtility modDbUtility
) : IDownloadableRecord
{
    public string Name { get; } = name;
    private string Url { get; } = url;
    private string NiceUrl { get; } = niceUrl;
    public string ArchiveName { get; } = archiveName;
    private string? Md5 { get; } = md5;
    public string DownloadPath => Path.Join(gammaDir, "downloads", ArchiveName);
    private string ExtractPath => Path.Join(gammaDir, "mods", outputDirName);
    private IList<string> Instructions { get; } = instructions;

    public virtual async Task DownloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (
                Path.Exists(DownloadPath)
                    && !string.IsNullOrWhiteSpace(Md5)
                    && await HashUtils.HashFile(
                        DownloadPath,
                        HashAlgorithmName.MD5,
                        pct =>
                            gammaProgress.OnProgressChanged(
                                new GammaProgress.GammaInstallProgressEventArgs(
                                    Name,
                                    "Check MD5",
                                    pct,
                                    NiceUrl
                                )
                            ),
                        cancellationToken
                    ) != Md5
                || !Path.Exists(DownloadPath)
            )
            {
                await modDbUtility.GetModDbLinkCurl(
                    Url,
                    DownloadPath,
                    pct =>
                        gammaProgress.OnProgressChanged(
                            new GammaProgress.GammaInstallProgressEventArgs(
                                Name,
                                "Download",
                                pct,
                                NiceUrl
                            )
                        ),
                    cancellationToken: cancellationToken
                );
                Downloaded = true;
            }
        }
        catch (Exception e)
        {
            throw new ModDbRecordException(
                $"""
                Error downloading ModDb record
                {ToString()}
                Exception Message: {e.Message}
                """,
                e
            );
        }
    }

    public virtual async Task ExtractAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Delete what was previously extracted
            if (Directory.Exists(ExtractPath))
            {
                DirUtils.NormalizePermissions(ExtractPath);
                DirUtils.RecursivelyDeleteDirectory(ExtractPath, doNotMatch: []);
            }

            Directory.CreateDirectory(ExtractPath);

            await archiveUtility.ExtractAsync(
                DownloadPath,
                ExtractPath,
                pct =>
                    gammaProgress.OnProgressChanged(
                        new GammaProgress.GammaInstallProgressEventArgs(
                            Name,
                            "Extract",
                            pct,
                            NiceUrl
                        )
                    ),
                ct: cancellationToken
            );

            ProcessInstructions.Process(ExtractPath, Instructions, cancellationToken);

            CleanExtractPath.Clean(ExtractPath);

            WriteAddonMetaIni.Write(ExtractPath, ArchiveName, NiceUrl);
        }
        catch (Exception e)
        {
            throw new ModDbRecordException(
                $"""
                Error extracting ModDb record
                {ToString()}
                Exception Message: {e.Message}
                """,
                e
            );
        }
    }

    public bool Downloaded { get; set; }

    public override string ToString() =>
        $"""
            Name: {Name}
            Archive Name: {ArchiveName}
            Url: {Url}
            NiceUrl: {NiceUrl}
            Download Path: {DownloadPath}
            Extract Path: {ExtractPath}
            Md5: {Md5}
            Downloaded: {Downloaded}
            Instructions: {string.Join(", ", Instructions)}
            """;
}

public class ModDbRecordException(string message, Exception innerException)
    : Exception(message, innerException);
