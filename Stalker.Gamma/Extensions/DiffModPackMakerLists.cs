using System.Text.RegularExpressions;
using Stalker.Gamma.Models;

namespace Stalker.Gamma.Extensions;

public static partial class DiffModPackMakerLists
{
    public static List<ModPackMakerRecordDiff> Diff(
        this List<ModPackMakerRecord> oldRecs,
        List<ModPackMakerRecord> newRecs
    )
    {
        List<ModPackMakerRecordDiff> diffs = [];

        // find removed and modified
        foreach (var old in oldRecs)
        {
            var matchingNewRec = newRecs.FirstOrDefault(x =>
                x.AddonName == old.AddonName && x.DlLink == old.DlLink
            );
            if (matchingNewRec is null)
            {
                diffs.Add(new ModPackMakerRecordDiff(DiffType.Removed, old, null));
            }
            else if (
                !string.Equals(
                    matchingNewRec.Md5ModDb,
                    old.Md5ModDb,
                    StringComparison.OrdinalIgnoreCase
                )
                || matchingNewRec.Instructions != old.Instructions
                || PatchRx().Match(matchingNewRec.Patch ?? "").Groups["author"].Value
                    != PatchRx().Match(old.Patch ?? "").Groups["author"].Value
            )
            {
                diffs.Add(new ModPackMakerRecordDiff(DiffType.Modified, old, matchingNewRec));
            }
        }

        // find added
        foreach (var newRec in newRecs)
        {
            var matchingOldRec = oldRecs.FirstOrDefault(x =>
                x.AddonName == newRec.AddonName && x.DlLink == newRec.DlLink
            );
            if (matchingOldRec is null)
            {
                diffs.Add(new ModPackMakerRecordDiff(DiffType.Added, null, newRec));
            }
        }

        return diffs;
    }

    [GeneratedRegex(@"^-\s+(?<author>.+)")]
    private static partial Regex PatchRx();
}

public class DiffModPackMakerListsException(string msg) : Exception(msg);

public enum DiffType
{
    Added,
    Modified,
    Removed,
}

public record ModPackMakerRecordDiff(
    DiffType DiffType,
    ModPackMakerRecord? OldListRecord,
    ModPackMakerRecord? NewListRecord
);
