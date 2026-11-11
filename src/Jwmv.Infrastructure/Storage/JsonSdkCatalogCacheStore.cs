using Jwmv.Core.Abstractions;
using Jwmv.Core.Models;

namespace Jwmv.Infrastructure.Storage;

public sealed class JsonSdkCatalogCacheStore(JwmvPaths paths) : ISdkCatalogCacheStore
{
    public Task<SdkCatalogCache?> LoadAsync(CancellationToken cancellationToken)
    {
        paths.EnsureCreated();
        return JsonFileHelper.ReadAsync<SdkCatalogCache>(paths.SdkCatalogCacheFilePath, cancellationToken);
    }

    public Task SaveAsync(SdkCatalogCache catalogCache, CancellationToken cancellationToken)
    {
        paths.EnsureCreated();
        return JsonFileHelper.WriteAsync(paths.SdkCatalogCacheFilePath, catalogCache, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(paths.SdkCatalogCacheFilePath))
        {
            File.Delete(paths.SdkCatalogCacheFilePath);
        }

        return Task.CompletedTask;
    }
}
