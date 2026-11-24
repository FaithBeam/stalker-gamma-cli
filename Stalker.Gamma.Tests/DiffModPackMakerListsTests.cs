using Stalker.Gamma.Extensions;
using Stalker.Gamma.Models;

namespace Stalker.Gamma.Tests;

public class DiffModPackMakerListsTests
{
    private static ModPackMakerRecord Rec(
        string name,
        int counter = 1,
        string link = "https://www.moddb.com/addons/start/1",
        string? md5 = "aaaa",
        string? instructions = "0",
        string? patch = "- Author"
    ) =>
        new()
        {
            Counter = counter,
            DlLink = link,
            AddonName = name,
            Md5ModDb = md5,
            Instructions = instructions,
            Patch = patch,
        };

    [Fact]
    public void Identical_lists_have_no_differences()
    {
        List<ModPackMakerRecord> old = [Rec("A", 1), Rec("B", 2)];
        List<ModPackMakerRecord> @new = [Rec("A", 1), Rec("B", 2)];

        Assert.Empty(old.Diff(@new));
    }

    [Fact]
    public void Finds_added_and_removed_addons()
    {
        List<ModPackMakerRecord> old = [Rec("Kept", 1), Rec("Removed", 2)];
        List<ModPackMakerRecord> @new = [Rec("Kept", 1), Rec("Added", 2)];

        var diffs = old.Diff(@new);

        Assert.Contains(
            diffs,
            d => d is { DiffType: DiffType.Removed, OldListRecord.AddonName: "Removed" }
        );
        Assert.Contains(
            diffs,
            d => d is { DiffType: DiffType.Added, NewListRecord.AddonName: "Added" }
        );
        Assert.Equal(2, diffs.Count);
    }

    [Fact]
    public void An_addon_is_matched_by_name_and_link()
    {
        List<ModPackMakerRecord> old = [Rec("A", link: "https://www.moddb.com/addons/start/1")];
        List<ModPackMakerRecord> @new = [Rec("A", link: "https://www.moddb.com/addons/start/2")];

        var diffs = old.Diff(@new);

        Assert.Equal([DiffType.Removed, DiffType.Added], diffs.Select(x => x.DiffType));
    }

    [Fact]
    public void A_new_md5_is_a_modification()
    {
        var diff = Assert.Single(
            new List<ModPackMakerRecord> { Rec("A", md5: "aaaa") }.Diff([Rec("A", md5: "bbbb")])
        );

        Assert.Equal(DiffType.Modified, diff.DiffType);
    }

    [Fact]
    public void Md5_comparison_ignores_case()
    {
        List<ModPackMakerRecord> old = [Rec("A", md5: "ABCDEF")];

        Assert.Empty(old.Diff([Rec("A", md5: "abcdef")]));
    }

    [Fact]
    public void New_instructions_are_a_modification()
    {
        List<ModPackMakerRecord> old = [Rec("A", instructions: "main")];

        var diff = Assert.Single(old.Diff([Rec("A", instructions: "main:optional")]));

        Assert.Equal(DiffType.Modified, diff.DiffType);
    }

    [Fact]
    public void Only_the_patch_author_is_compared()
    {
        List<ModPackMakerRecord> old = [Rec("A", patch: "- Author")];

        // extra whitespace after the dash is formatting, not a change
        Assert.Empty(old.Diff([Rec("A", patch: "-   Author")]));
        Assert.Equal(
            DiffType.Modified,
            Assert.Single(old.Diff([Rec("A", patch: "- Other")])).DiffType
        );
    }

    [Fact]
    public void An_addon_that_moved_to_another_line_is_a_modification()
    {
        // removing the first addon moves the second up a line, and its folder is named after
        // its line number, e.g. "2- B - Author" becomes "1- B - Author"
        List<ModPackMakerRecord> old = [Rec("A", 1), Rec("B", 2)];
        List<ModPackMakerRecord> @new = [Rec("B", 1)];

        var diffs = old.Diff(@new);

        Assert.Contains(
            diffs,
            d => d is { DiffType: DiffType.Removed, OldListRecord.AddonName: "A" }
        );
        var moved = Assert.Single(diffs, d => d.DiffType == DiffType.Modified);
        Assert.Equal("B", moved.NewListRecord!.AddonName);
        Assert.Equal(2, moved.OldListRecord!.Counter);
        Assert.Equal(1, moved.NewListRecord.Counter);
    }

    [Theory]
    [InlineData(0, 2, false)]
    [InlineData(2, 0, false)]
    [InlineData(2, 2, false)]
    [InlineData(2, 3, true)]
    public void IsRenumbered_ignores_records_without_a_line_number(
        int oldCounter,
        int newCounter,
        bool expected
    ) =>
        Assert.Equal(
            expected,
            DiffModPackMakerLists.IsRenumbered(Rec("A", oldCounter), Rec("A", newCounter))
        );
}
