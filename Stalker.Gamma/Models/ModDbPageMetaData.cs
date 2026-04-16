namespace Stalker.Gamma.Models;

public class ModDbPageMetadata
{
    public required DateTimeOffset Added { get; set; }

    public required string Category { get; set; }

    public string? Credits { get; set; }

    public required long Downloads { get; set; }

    public required string Filename { get; set; }

    public string? Licence { get; set; }

    public string? Location { get; set; }

    public required string Md5Hash { get; set; }

    public required long Size { get; set; }

    // TODO : Collect MirrorUrl
    // public required string MirrorUrl { get; set; }

    public required DateTimeOffset Updated { get; set; }

    public required string Uploader { get; set; }

    public required string Url { get; set; }

    public override string ToString() =>
        $"{nameof(ModDbPageMetadata)} {{ "
        + $"{nameof(Added)} = {Added:o}, "
        + $"{nameof(Category)} = {Category}, "
        + $"{nameof(Credits)} = {Credits}, "
        + $"{nameof(Downloads)} = {Downloads}, "
        + $"{nameof(Filename)} = {Filename}, "
        + $"{nameof(Licence)} = {Licence}, "
        + $"{nameof(Location)} = {Location}, "
        + $"{nameof(Md5Hash)} = {Md5Hash}, "
        + $"{nameof(Size)} = {Size}, "
        + $"{nameof(Updated)} = {Updated:o}, "
        + $"{nameof(Uploader)} = {Uploader}, "
        + $"{nameof(Url)} = {Url} "
        + "}";
}
