using Jwmv.Core.Models;

namespace Jwmv.Core.Abstractions;

public interface ISdkPackageDetailsProvider
{
    Task<SdkPackage> GetPackageDetailsAsync(SdkPackage package, CancellationToken cancellationToken);
}
