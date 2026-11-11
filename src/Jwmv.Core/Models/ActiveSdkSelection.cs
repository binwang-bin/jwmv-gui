namespace Jwmv.Core.Models;

public sealed record ActiveSdkSelection
{
    public required string CandidateName { get; init; }
    public string? Version { get; init; }
    public string? Alias { get; init; }
    public string? HomeDirectory { get; init; }
    public string? BinDirectory { get; init; }
    public JavaActivationSource Source { get; init; }
    public string? ProjectFilePath { get; init; }

    public bool IsResolved => !string.IsNullOrWhiteSpace(Version) && !string.IsNullOrWhiteSpace(HomeDirectory);
}
