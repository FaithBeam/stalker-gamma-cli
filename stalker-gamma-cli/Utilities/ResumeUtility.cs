using Serilog;
using Stalker.Gamma.Models;

namespace stalker_gamma_cli.Utilities;

public static class ResumeUtility
{
    /// <summary>
    /// Load the resume file from a previous failed run if resume is true, otherwise start fresh.
    /// </summary>
    public static async Task<ResumeState> LoadAsync(
        ILogger logger,
        bool resume,
        string gamma,
        string command,
        CancellationToken cancellationToken
    )
    {
        var path = ResumeState.GetPath(gamma);
        if (!resume)
        {
            return new ResumeState { Command = command, FilePath = path };
        }

        if (!File.Exists(path))
        {
            logger.Warning("No resume file found at {ResumePath}. Starting from scratch.", path);
            return new ResumeState { Command = command, FilePath = path };
        }

        var state = await ResumeState.ReadAsync(path, cancellationToken);
        if (state.Command != command)
        {
            throw new ResumeStateException(
                $"Resume file {path} was created by `{state.Command}`, not `{command}`. Run `{state.Command} --resume` or run without --resume."
            );
        }
        logger.Information("Resuming from {ResumePath}", path);
        state.FilePath = path;
        return state;
    }

    /// <summary>
    /// Write the resume file so the next run can continue with --resume.
    /// </summary>
    public static void Write(ILogger logger, ResumeState state, string gamma)
    {
        if (state.IsEmpty)
        {
            return;
        }

        var path = ResumeState.GetPath(gamma);
        try
        {
            state.Write(path);
            logger.Information(
                "Progress saved to {ResumePath}. Run `{Command} --resume` to continue where this left off.",
                path,
                state.Command
            );
        }
        catch (Exception e)
        {
            logger.Error(e, "Unable to write resume file {ResumePath}", path);
        }
    }

    public static void Delete(string gamma)
    {
        var path = ResumeState.GetPath(gamma);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
