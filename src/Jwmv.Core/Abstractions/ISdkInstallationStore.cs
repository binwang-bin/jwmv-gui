using Jwmv.Core.Models;

namespace Jwmv.Core.Abstractions;

public interface ISdkInstallationStore
{
    Task<IReadOnlyList<InstalledSdkVersion>> GetInstalledVersionsAsync(string? candidateName, CancellationToken cancellationToken);
    Task<InstalledSdkVersion?> FindAsync(string candidateName, string alias, CancellationToken cancellationToken);
    Task SaveAsync(InstalledSdkVersion installedVersion, CancellationToken cancellationToken);
    Task DeleteAsync(string candidateName, string alias, CancellationToken cancellationToken);
}
