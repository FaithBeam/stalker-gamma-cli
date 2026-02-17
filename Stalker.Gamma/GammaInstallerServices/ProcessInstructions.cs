using Stalker.Gamma.Utilities;

namespace Stalker.Gamma.GammaInstallerServices;

internal static class ProcessInstructions
{
    internal static void Process(
        string extractPath,
        IList<string> instructions,
        CancellationToken cancellationToken
    )
    {
        foreach (var i in instructions)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            if (Path.Exists(Path.Join(extractPath, i, "gamedata")))
            {
                DirUtils.CopyDirectory(
                    Path.Join(extractPath, i),
                    extractPath,
                    moveFile: true,
                    cancellationToken: cancellationToken
                );
            }
            else
            {
                Directory.CreateDirectory(Path.Join(extractPath, "gamedata"));
                if (Directory.Exists(Path.Join(extractPath, i)))
                {
                    DirUtils.CopyDirectory(
                        Path.Join(extractPath, i),
                        Path.Join(extractPath, "gamedata"),
                        moveFile: true,
                        cancellationToken: cancellationToken
                    );
                }
            }
        }
    }
}
