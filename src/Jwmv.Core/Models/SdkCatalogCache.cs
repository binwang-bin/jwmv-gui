namespace Jwmv.Core.Models;

public sealed record SdkCatalogCache
{
    public DateTimeOffset RefreshedAtUtc { get; init; }
    public Dictionary<string, List<SdkPackage>> PackagesByCandidate { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
