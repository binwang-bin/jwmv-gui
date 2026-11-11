namespace Jwmv.Core.Models;

public sealed record InstalledSdkVersion
{
    public required string CandidateName { get; init; }
    public required string Version { get; init; }
    public required string Alias { get; init; }
    public required string DisplayName { get; init; }
    public string? Distribution { get; init; }
    public string? DistributionAlias { get; init; }
    public required string InstallDirectory { get; init; }
    public required string HomeDirectory { get; init; }
    public required string BinDirectory { get; init; }
    public required string HomeEnvironmentVariable { get; init; }
    public required string PrimaryExecutableName { get; init; }
    public required string ArchiveType { get; init; }
    public required string PackageFileName { get; init; }
    public required string SourcePackageId { get; init; }
    public string? Checksum { get; init; }
    public string? ChecksumType { get; init; }
    public DateTimeOffset InstalledAtUtc { get; init; }

    /// <summary>
    /// True when this entry points at an existing local installation outside ~/.jwmv/candidates.
    /// Uninstall removes only the jwmv manifest and never deletes the external directory.
    /// </summary>
    public bool IsExternal { get; init; }
}
