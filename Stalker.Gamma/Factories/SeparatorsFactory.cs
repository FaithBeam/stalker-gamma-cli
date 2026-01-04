using Stalker.Gamma.Models;

namespace Stalker.Gamma.Factories;

public interface ISeparatorsFactory
{
    List<ISeparator> Create(IList<ModPackMakerRecord> records);
}

public class SeparatorsFactory : ISeparatorsFactory
{
    public List<ISeparator> Create(IList<ModPackMakerRecord> records) =>
        records
            .Select((r, idx) => new { r, idx })
            .Where(r =>
                string.IsNullOrWhiteSpace(r.r.AddonName)
                && string.IsNullOrWhiteSpace(r.r.Instructions)
                && string.IsNullOrWhiteSpace(r.r.Md5ModDb)
                && string.IsNullOrWhiteSpace(r.r.ZipName)
                && string.IsNullOrWhiteSpace(r.r.ModDbUrl)
                && string.IsNullOrWhiteSpace(r.r.Patch)
            )
            .Select(r => new Separator
            {
                Name = $"{r.r.DlLink} Separator",
                FolderName = $"{r.idx + 1}- {r.r.DlLink}_separator",
            })
            .Cast<ISeparator>()
            .ToList();
}
