using System.Runtime.Serialization;

namespace Stalker.Gamma.GammaInstallerServices;

public interface IGammaProgress
{
    int TotalMods { get; set; }
    event EventHandler<GammaProgress.GammaInstallProgressEventArgs>? ProgressChanged;
    event EventHandler<GammaProgress.GammaInstallDebugProgressEventArgs>? DebugProgressChanged;
}

public static class GammaProgressExtensions
{
    extension(GammaProgressType e)
    {
        public string GetHumanReadableString() =>
            e switch
            {
                GammaProgressType.Download => "Download",
                GammaProgressType.Extract => "Extract",
                GammaProgressType.Expand => "Expand",
                GammaProgressType.CheckMd5 => "Check MD5",
                GammaProgressType.Skipped => "Skipped",
                _ => throw new ArgumentOutOfRangeException(nameof(e), e, null),
            };
    }
}

public class GammaProgress : IGammaProgress
{
    private int _completedMods;

    public int TotalMods { get; set; }

    internal void IncrementCompletedMods() => Interlocked.Increment(ref _completedMods);

    internal void Reset()
    {
        _completedMods = 0;
        TotalMods = 0;
    }

    public event EventHandler<GammaInstallProgressEventArgs>? ProgressChanged;
    public event EventHandler<GammaInstallDebugProgressEventArgs>? DebugProgressChanged;

    internal void OnDebugProgressChanged(GammaInstallDebugProgressEventArgs e) =>
        DebugProgressChanged?.Invoke(this, e);

    internal void OnProgressChanged(GammaInstallProgressEventArgs e)
    {
        e.Complete = _completedMods;
        e.Total = TotalMods;
        ProgressChanged?.Invoke(this, e);
    }

    public class GammaInstallDebugProgressEventArgs
    {
        public string? Text { get; set; }
    }

    public class GammaInstallProgressEventArgs
        : EventArgs,
            IEquatable<GammaInstallProgressEventArgs>
    {
        public required string Name { get; init; }
        public required GammaProgressType ProgressType { get; init; }
        public required double Progress { get; init; }
        public required string Url { get; init; }
        public required string ArchiveName { get; init; }
        public required string DownloadPath { get; init; }
        public required string ExtractPath { get; init; }
        public int Complete { get; set; }
        public int Total { get; set; }

        public bool Equals(GammaInstallProgressEventArgs? other)
        {
            if (other is null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return Name == other.Name
                && ProgressType == other.ProgressType
                && Progress.Equals(other.Progress)
                && Url == other.Url
                && ArchiveName == other.ArchiveName
                && DownloadPath == other.DownloadPath
                && ExtractPath == other.ExtractPath;
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != GetType())
                return false;
            return Equals((GammaInstallProgressEventArgs)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                Name,
                ProgressType,
                Progress,
                Url,
                ArchiveName,
                DownloadPath,
                ExtractPath
            );
        }
    }
}

public enum GammaProgressType
{
    Download,
    Extract,
    Expand,
    CheckMd5,
    Skipped,
}
