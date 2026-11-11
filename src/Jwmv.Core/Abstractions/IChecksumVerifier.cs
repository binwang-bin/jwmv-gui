using Jwmv.Core.Models;

namespace Jwmv.Core.Abstractions;

public interface IChecksumVerifier
{
    Task<ChecksumVerificationResult> VerifyAsync(string filePath, string expectedChecksum, string checksumType, CancellationToken cancellationToken);
}
