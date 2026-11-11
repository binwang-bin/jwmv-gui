using Jwmv.Core.Models;

namespace Jwmv.Core.Abstractions;

public interface ISdkEnvironmentService
{
    Task ApplyDefaultAsync(InstalledSdkVersion installedVersion, CancellationToken cancellationToken);
    Task ClearDefaultAsync(string candidateName, CancellationToken cancellationToken);
}
