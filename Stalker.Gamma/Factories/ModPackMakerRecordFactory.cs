using Stalker.Gamma.Models;

namespace Stalker.Gamma.Factories;

public interface IModListRecordFactory
{
    List<ModPackMakerRecord> Create(string modpackMakerTxt);
}

public class ModPackMakerRecordFactory : IModListRecordFactory
{
    public List<ModPackMakerRecord> Create(string modpackMakerTxt) =>
        modpackMakerTxt
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(
                (line, idx) =>
                {
                    var lineSplit = line.Split('\t');
                    return new ModPackMakerRecord
                    {
                        Counter = ++idx,
                        DlLink = lineSplit[0].Trim(),
                        Instructions = lineSplit.ElementAtOrDefault(1)?.Trim(),
                        Patch = lineSplit.ElementAtOrDefault(2)?.Trim(),
                        AddonName = lineSplit.ElementAtOrDefault(3)?.Trim(),
                        ModDbUrl = lineSplit.ElementAtOrDefault(4)?.Trim(),
                        ZipName = lineSplit.ElementAtOrDefault(5)?.Trim(),
                        Md5ModDb = lineSplit.ElementAtOrDefault(6)?.Trim(),
                    };
                }
            )
            .ToList();
}
