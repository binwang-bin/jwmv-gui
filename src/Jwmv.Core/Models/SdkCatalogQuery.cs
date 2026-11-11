namespace Jwmv.Core.Models;

public sealed record SdkCatalogQuery
{
    public string? CandidateName { get; init; }
    public string? VersionFilter { get; init; }
    public bool ForceRefresh { get; init; }
}
