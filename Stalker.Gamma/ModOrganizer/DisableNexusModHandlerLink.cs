namespace Stalker.Gamma.ModOrganizer;

public static class DisableNexusModHandlerLink
{
    public static async Task DisableAsync(string gammaPath)
    {
        await File.WriteAllTextAsync(
            Path.Join(gammaPath, "nxmhandler.ini"),
            """
            [General]
            noregister=true

            """
        );
    }
}
