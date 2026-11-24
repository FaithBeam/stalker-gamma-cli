using Stalker.Gamma.Models;

namespace Stalker.Gamma.Tests;

public class ResumeStateTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Registered_records_start_at_download()
    {
        var state = new ResumeState();

        state.Register("Addon | addon.zip");

        Assert.Equal(ResumeStatus.Download, state.GetStatus("Addon | addon.zip"));
        Assert.Null(state.GetStatus("Unknown | unknown.zip"));
    }

    [Fact]
    public void Registering_again_keeps_a_status_loaded_from_the_file()
    {
        var state = new ResumeState();
        state.SetStatus("Addon | addon.zip", ResumeStatus.Complete);

        state.Register("Addon | addon.zip");

        Assert.True(state.IsComplete("Addon | addon.zip"));
    }

    [Fact]
    public async Task Round_trips_through_the_file()
    {
        var path = _dir.Join("gamma", ResumeState.FileName);
        var state = new ResumeState { Command = "full-install" };
        state.SetStatus("B | b.zip", ResumeStatus.Complete);
        state.SetStatus("A | a.zip", ResumeStatus.Download);
        state.SetStatus("C | c.zip", ResumeStatus.Extract);
        // a GitHub repo record has no archive name, so its key ends in a space
        state.SetStatus("Stalker_GAMMA | ", ResumeStatus.Download);

        state.Write(path);
        var read = await ResumeState.ReadAsync(path);

        Assert.Equal("full-install", read.Command);
        Assert.Equal(ResumeStatus.Complete, read.GetStatus("B | b.zip"));
        Assert.Equal(ResumeStatus.Download, read.GetStatus("A | a.zip"));
        Assert.Equal(ResumeStatus.Extract, read.GetStatus("C | c.zip"));
        Assert.Equal(ResumeStatus.Download, read.GetStatus("Stalker_GAMMA | "));
        Assert.False(File.Exists($"{path}.tmp"));
    }

    [Fact]
    public void File_lists_records_by_status_then_name()
    {
        var path = _dir.Join(ResumeState.FileName);
        var state = new ResumeState { Command = "update apply" };
        state.SetStatus("Z | z.zip", ResumeStatus.Complete);
        state.SetStatus("Y | y.zip", ResumeStatus.Download);
        state.SetStatus("X | x.zip", ResumeStatus.Download);

        state.Write(path);

        Assert.Equal(
            [
                "# stalker-gamma resume file. Run the same command with --resume to continue.",
                "# command: update apply",
                "# status\trecord",
                "Download\tX | x.zip",
                "Download\tY | y.zip",
                "Complete\tZ | z.zip",
            ],
            File.ReadAllLines(path)
        );
    }

    [Fact]
    public void Each_status_change_is_saved_when_a_path_is_set()
    {
        var path = _dir.Join(ResumeState.FileName);
        var state = new ResumeState { Command = "full-install", FilePath = path };

        state.SetStatus("A | a.zip", ResumeStatus.Extract);

        Assert.Contains("Extract\tA | a.zip", File.ReadAllLines(path));
    }

    [Fact]
    public async Task Invalid_line_is_an_error()
    {
        var path = _dir.WriteFile(
            ResumeState.FileName,
            "# command: full-install\nDone\tA | a.zip\n"
        );

        var e = await Assert.ThrowsAsync<ResumeStateException>(() => ResumeState.ReadAsync(path));

        Assert.Contains("Done\tA | a.zip", e.Message);
    }

    [Fact]
    public async Task Blank_lines_and_comments_are_ignored()
    {
        var path = _dir.WriteFile(
            ResumeState.FileName,
            "# command: full-install\n\n# a comment\nComplete\tA | a.zip\n\n"
        );

        var state = await ResumeState.ReadAsync(path);

        Assert.True(state.IsComplete("A | a.zip"));
        Assert.False(state.IsEmpty);
    }

    [Fact]
    public void GetPath_is_in_the_gamma_folder() =>
        Assert.Equal(Path.Join(_dir.Path, "resume.txt"), ResumeState.GetPath(_dir.Path));
}
