using System.Text.Json.Serialization;

namespace Jwmv.Core.Models;

public sealed record SdkPackage
{
    public required string Id { get; init; }
    public required string CandidateName { get; init; }
    public required string Version { get; init; }
    public required string Alias { get; init; }
    public required string DisplayName { get; init; }
    public string? Distribution { get; init; }
    public string? DistributionAlias { get; init; }
    public required string ArchiveType { get; init; }
    public required string FileName { get; init; }
    public required Uri DownloadUri { get; init; }
    public Uri? PackageInfoUri { get; init; }
    public Uri? ChecksumUri { get; init; }
    public string? Checksum { get; init; }
    public string? ChecksumType { get; init; }
    public string ReleaseStatus { get; init; } = "ga";
    public string SupportTerm { get; init; } = string.Empty;
    public long Size { get; init; }

    [JsonIgnore]
    public string CandidateVersion => $"{CandidateName} {Alias}";
}
