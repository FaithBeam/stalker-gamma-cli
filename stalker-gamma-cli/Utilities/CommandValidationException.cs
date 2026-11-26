namespace stalker_gamma_cli.Utilities;

/// <summary>
/// A command can't run, e.g. there's no active profile. Only its message is shown, without a
/// stack trace, and commands run with --json still write their result message.
/// </summary>
public class CommandValidationException(string message) : Exception(message);
