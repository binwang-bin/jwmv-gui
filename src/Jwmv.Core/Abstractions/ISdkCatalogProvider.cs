using Jwmv.Core.Models;

namespace Jwmv.Core.Abstractions;

public interface ISdkCatalogProvider
{
    SdkCandidate Candidate { get; }
    Task<IReadOnlyList<SdkPackage>> GetPackagesAsync(CancellationToken cancellationToken);
}
