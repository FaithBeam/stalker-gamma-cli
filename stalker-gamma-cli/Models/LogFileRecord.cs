namespace stalker_gamma_cli.Models;

public record LogFileRecord
{
    public required string Operation { get; set; }
    public required string ArchiveName { get; set; }
    public required string DownloadPath { get; set; }
    public required string ExtractPath { get; set; }
    public required string Url { get; set; }
}
