namespace Stalker.Gamma.GammaInstallerServices.SpecialRepos;

public class SpecialRepoException(string message, Exception innerException)
    : Exception(message, innerException);
