namespace Jwmv.Core.Models;

public sealed record SdkCandidate
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required Uri WebsiteUri { get; init; }
    public required string HomeEnvironmentVariable { get; init; }
    public required string PrimaryExecutableName { get; init; }
    public string BinRelativePath { get; init; } = "bin";
    public string? LatestVersion { get; init; }
}
