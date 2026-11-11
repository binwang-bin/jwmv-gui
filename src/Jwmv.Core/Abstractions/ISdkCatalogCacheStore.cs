using Jwmv.Core.Models;

namespace Jwmv.Core.Abstractions;

public interface ISdkCatalogCacheStore
{
    Task<SdkCatalogCache?> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(SdkCatalogCache catalogCache, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}
