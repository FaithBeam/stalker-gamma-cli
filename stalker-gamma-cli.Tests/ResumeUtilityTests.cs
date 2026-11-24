using Serilog;
using Serilog.Core;
using Serilog.Events;
using stalker_gamma_cli.Utilities;
using Stalker.Gamma.Models;

namespace stalker_gamma_cli.Tests;

public class ResumeUtilityTests : IDisposable
{
    private readonly TempDir _gamma = new();
    private readonly LogSink _log = new();
    private readonly ILogger _logger;

    public ResumeUtilityTests() =>
        _logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(_log)
            .CreateLogger();

    public void Dispose() => _gamma.Dispose();

    private string ResumePath => ResumeState.GetPath(_gamma.Path);

    private void WriteResumeFile(string command, params (string Key, ResumeStatus Status)[] records)
    {
        var state = new ResumeState { Command = command };
        foreach (var (key, status) in records)
        {
            state.SetStatus(key, status);
        }
        state.Write(ResumePath);
    }

    [Fact]
    public async Task Without_resume_a_leftover_file_is_ignored()
    {
        WriteResumeFile("full-install", ("A | a.zip", ResumeStatus.Complete));

        var state = await ResumeUtility.LoadAsync(
            _logger,
            false,
            _gamma.Path,
            "full-install",
            default
        );

        Assert.True(state.IsEmpty);
        Assert.Equal("full-install", state.Command);
        Assert.Equal(ResumePath, state.FilePath);
    }

    [Fact]
    public async Task Resume_continues_from_the_file()
    {
        WriteResumeFile("full-install", ("A | a.zip", ResumeStatus.Complete));

        var state = await ResumeUtility.LoadAsync(
            _logger,
            true,
            _gamma.Path,
            "full-install",
            default
        );

        Assert.True(state.IsComplete("A | a.zip"));
        Assert.Equal(ResumePath, state.FilePath);
    }

    [Fact]
    public async Task Resume_without_a_file_starts_over_with_a_warning()
    {
        var state = await ResumeUtility.LoadAsync(
            _logger,
            true,
            _gamma.Path,
            "full-install",
            default
        );

        Assert.True(state.IsEmpty);
        Assert.Contains(_log.Events, e => e.Level == LogEventLevel.Warning);
    }

    [Fact]
    public async Task Resume_file_from_another_command_is_an_error()
    {
        WriteResumeFile("update apply", ("A | a.zip", ResumeStatus.Complete));

        var e = await Assert.ThrowsAsync<ResumeStateException>(() =>
            ResumeUtility.LoadAsync(_logger, true, _gamma.Path, "full-install", default)
        );

        Assert.Contains("update apply --resume", e.Message);
    }

    [Fact]
    public void Write_saves_progress_for_the_next_run()
    {
        var state = new ResumeState { Command = "full-install" };
        state.SetStatus("A | a.zip", ResumeStatus.Extract);

        ResumeUtility.Write(_logger, state, _gamma.Path);

        Assert.Contains("Extract\tA | a.zip", File.ReadAllLines(ResumePath));
    }

    [Fact]
    public void Write_skips_an_empty_state()
    {
        ResumeUtility.Write(_logger, new ResumeState { Command = "full-install" }, _gamma.Path);

        Assert.False(File.Exists(ResumePath));
    }

    [Fact]
    public void Delete_removes_the_file()
    {
        WriteResumeFile("full-install", ("A | a.zip", ResumeStatus.Complete));

        ResumeUtility.Delete(_gamma.Path);
        ResumeUtility.Delete(_gamma.Path);

        Assert.False(File.Exists(ResumePath));
    }

    private class LogSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
