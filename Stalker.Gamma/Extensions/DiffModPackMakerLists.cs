using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Stalker.Gamma.Models;

namespace Stalker.Gamma.Extensions;

public static partial class DiffModPackMakerLists
{
    public static List<ModDbDiff> Diff(this ModDb[] oldRecs, ModDb[] newRecs)
    {
        List<ModDbDiff> diffs = [];

        // find removed and modified
        foreach (var old in oldRecs)
        {
            var matchingNewRec = newRecs.FirstOrDefault(x =>
                x.Name == old.Name && x.DownloadUrl == old.DownloadUrl
            );
            if (matchingNewRec is null)
            {
                diffs.Add(new ModDbDiff(DiffType.Removed, old, null));
            }
            else if (
                !string.Equals(matchingNewRec.Md5, old.Md5, StringComparison.OrdinalIgnoreCase)
                || matchingNewRec.Instructions != old.Instructions
            )
            {
                diffs.Add(new ModDbDiff(DiffType.Modified, old, matchingNewRec));
            }
        }

        // find added
        foreach (var newRec in newRecs)
        {
            var matchingOldRec = oldRecs.FirstOrDefault(x =>
                x.Name == newRec.Name && x.DownloadUrl == newRec.DownloadUrl
            );
            if (matchingOldRec is null)
            {
                diffs.Add(new ModDbDiff(DiffType.Added, null, newRec));
            }
        }

        return diffs;
    }

    public static List<GithubDiff> Diff(this Github[] oldRecs, Github[] newRecs)
    {
        List<GithubDiff> diffs = [];

        // find removed and modified
        foreach (var old in oldRecs)
        {
            var matchingNewRec = newRecs.FirstOrDefault(x =>
                x.Name == old.Name && x.DownloadUrl == old.DownloadUrl
            );
            if (matchingNewRec is null)
            {
                diffs.Add(new GithubDiff(DiffType.Removed, old, null));
            }
            else if (
                !string.Equals(matchingNewRec.Md5, old.Md5, StringComparison.OrdinalIgnoreCase)
                || matchingNewRec.Instructions != old.Instructions
            )
            {
                diffs.Add(new GithubDiff(DiffType.Modified, old, matchingNewRec));
            }
        }

        // find added
        foreach (var newRec in newRecs)
        {
            var matchingOldRec = oldRecs.FirstOrDefault(x =>
                x.Name == newRec.Name && x.DownloadUrl == newRec.DownloadUrl
            );
            if (matchingOldRec is null)
            {
                diffs.Add(new GithubDiff(DiffType.Added, null, newRec));
            }
        }

        return diffs;
    }

    [GeneratedRegex(@"^-\s+(?<author>.+)")]
    private static partial Regex PatchRx();

    public static async Task<List<ModDbDiff>> DiffAsync(
        this List<ModDb> oldRecs,
        ConfiguredCancelableAsyncEnumerable<Task<ModDb>> remoteRepoModPackMakerRecs
    )
    {
        List<ModDbDiff> diffs = [];

        List<ModDb> remoteRecs = [];

        await foreach (var rec in remoteRepoModPackMakerRecs)
        {
            remoteRecs.Add(await rec);
        }

        // find removed and modified
        foreach (var old in oldRecs)
        {
            var matchingNewRec = remoteRecs.FirstOrDefault(x =>
                x.Name == old.Name && x.DownloadUrl == old.DownloadUrl
            );
            if (matchingNewRec is null)
            {
                diffs.Add(new ModDbDiff(DiffType.Removed, old, null));
            }
            else if (
                !string.Equals(matchingNewRec.Md5, old.Md5, StringComparison.OrdinalIgnoreCase)
                || matchingNewRec.Instructions != old.Instructions
            )
            {
                diffs.Add(new ModDbDiff(DiffType.Modified, old, matchingNewRec));
            }
        }

        return diffs;
    }
}

public class DiffModPackMakerListsException(string msg) : Exception(msg);

public enum DiffType
{
    Added,
    Modified,
    Removed,
}

public record ModDbDiff(DiffType DiffType, ModDb? OldListRecord, ModDb? NewListRecord);

public record GithubDiff(DiffType DiffType, Github? OldListRecord, Github? NewListRecord);
