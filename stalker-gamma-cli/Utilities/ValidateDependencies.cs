namespace stalker_gamma_cli.Utilities;

public static class ValidateDependencies
{
    /// <exception cref="CommandValidationException">A native dependency wasn't found</exception>
    public static void Validate(UtilitiesReady utilitiesReady)
    {
        if (!utilitiesReady.IsReady)
        {
            throw new CommandValidationException(
                $"""
                Dependency not found:
                {utilitiesReady.NotReadyReason}
                """
            );
        }
    }
}
