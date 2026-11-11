using Jwmv.Core.Models;

namespace Jwmv.Core.Abstractions;

public interface ISdkVersionManager
{
    Task<IReadOnlyList<SdkCandidate>> ListCandidatesAsync(string? filter, CancellationToken cancellationToken);
    Task<IReadOnlyList<SdkPackage>> ListAvailableAsync(SdkCatalogQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<InstalledSdkVersion>> ListInstalledAsync(string? candidateName, CancellationToken cancellationToken);
    Task<InstallSdkResult> InstallAsync(InstallSdkRequest request, IProgress<InstallProgressUpdate>? progress, CancellationToken cancellationToken);
    Task<IReadOnlyList<InstallSdkResult>> ImportLocalAsync(ImportLocalSdkRequest request, CancellationToken cancellationToken);
    Task UninstallAsync(string candidateName, string version, CancellationToken cancellationToken);
    Task<InstalledSdkVersion> SetDefaultAsync(string candidateName, string version, CancellationToken cancellationToken);
    Task<ActiveSdkSelection> ResolveCurrentAsync(string candidateName, string? workingDirectory, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActiveSdkSelection>> ResolveAllCurrentAsync(string? workingDirectory, CancellationToken cancellationToken);
    Task<string?> GetHomeAsync(string candidateName, string? version, CancellationToken cancellationToken);
    Task<string> BuildUseShellScriptAsync(string candidateName, string version, ShellKind shellKind, CancellationToken cancellationToken);
    Task<string> BuildEnvShellScriptAsync(string? workingDirectory, ShellKind shellKind, CancellationToken cancellationToken);
    Task<string> BuildShellInitScriptAsync(ShellKind shellKind, CancellationToken cancellationToken);
    Task<ProjectSdkConfiguration?> FindProjectConfigurationAsync(string? workingDirectory, CancellationToken cancellationToken);
    Task<IReadOnlyList<InstallSdkResult>> InstallProjectMissingAsync(string? workingDirectory, bool forceCatalogRefresh, IProgress<InstallProgressUpdate>? progress, CancellationToken cancellationToken);
    Task RefreshCatalogAsync(CancellationToken cancellationToken);
    Task<AppConfig> GetConfigAsync(CancellationToken cancellationToken);
}
