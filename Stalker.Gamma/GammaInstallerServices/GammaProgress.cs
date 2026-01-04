namespace Stalker.Gamma.GammaInstallerServices;

public interface IGammaProgress
{
    int TotalMods { get; set; }
    event EventHandler<GammaProgress.GammaInstallProgressEventArgs>? ProgressChanged;
    event EventHandler<GammaProgress.GammaInstallDebugProgressEventArgs>? DebugProgressChanged;
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

    public class GammaInstallProgressEventArgs(
        string name,
        string progressType,
        double progress,
        string url
    ) : EventArgs
    {
        public string Name { get; } = name;
        public string ProgressType { get; } = progressType;
        public double Progress { get; } = progress;
        public string Url { get; } = url;
        public int Complete { get; set; }
        public int Total { get; set; }
    }
}
