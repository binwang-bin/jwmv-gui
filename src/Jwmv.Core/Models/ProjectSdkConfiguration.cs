namespace Jwmv.Core.Models;

public sealed record ProjectSdkConfiguration
{
    public required IReadOnlyDictionary<string, string> Versions { get; init; }
    public required string FilePath { get; init; }

    public string? GetVersion(string candidateName) =>
        Versions.TryGetValue(candidateName, out var version) ? version : null;
}
