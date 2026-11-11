using Jwmv.Core;
using Jwmv.Core.Models;
using Jwmv.Core.Utilities;

namespace Jwmv.Infrastructure.Storage;

internal static class InstalledVersionMapper
{
    public static InstalledJavaVersion ToJava(InstalledSdkVersion sdk)
    {
        var distributionAlias = string.IsNullOrWhiteSpace(sdk.DistributionAlias) ? "local" : sdk.DistributionAlias;
        var distribution = string.IsNullOrWhiteSpace(sdk.Distribution) ? "Local" : sdk.Distribution;
        return new InstalledJavaVersion
        {
            Alias = string.IsNullOrWhiteSpace(sdk.Alias)
                ? JavaIdentifier.BuildAlias(sdk.Version, distributionAlias)
                : sdk.Alias,
            Distribution = distribution,
            DistributionAlias = distributionAlias,
            JavaVersion = sdk.Version,
            DistributionVersion = sdk.Version,
            InstallDirectory = sdk.InstallDirectory,
            JavaHome = sdk.HomeDirectory,
            ArchiveType = sdk.ArchiveType,
            PackageFileName = sdk.PackageFileName,
            SourcePackageId = sdk.SourcePackageId,
            InstalledAtUtc = sdk.InstalledAtUtc
        };
    }

    public static InstalledSdkVersion ToSdk(InstalledJavaVersion legacy) =>
        new()
        {
            CandidateName = JwmvConstants.CandidateName,
            Version = legacy.JavaVersion,
            Alias = legacy.Alias,
            DisplayName = legacy.DisplayAlias,
            Distribution = legacy.Distribution,
            DistributionAlias = legacy.DistributionAlias,
            InstallDirectory = legacy.InstallDirectory,
            HomeDirectory = legacy.JavaHome,
            BinDirectory = Path.Combine(legacy.JavaHome, "bin"),
            HomeEnvironmentVariable = "JAVA_HOME",
            PrimaryExecutableName = OperatingSystem.IsWindows() ? "java.exe" : "java",
            ArchiveType = legacy.ArchiveType,
            PackageFileName = legacy.PackageFileName,
            SourcePackageId = legacy.SourcePackageId,
            InstalledAtUtc = legacy.InstalledAtUtc
        };
}
