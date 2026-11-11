using System.Text;
using Jwmv.Core;
using Jwmv.Core.Abstractions;
using Jwmv.Core.Exceptions;
using Jwmv.Core.Models;
using Jwmv.Core.Utilities;
using Jwmv.Infrastructure.Shell;
using Jwmv.Infrastructure.Windows;

namespace Jwmv.Infrastructure.Services;

public sealed class SdkVersionManager(
    IAppContext appContext,
    IClock clock,
    IConfigStore configStore,
    ISdkCatalogCacheStore catalogCacheStore,
    IEnumerable<ISdkCatalogProvider> catalogProviders,
    ISdkInstallationStore installationStore,
    IArchiveDownloader archiveDownloader,
    IArchiveExtractor archiveExtractor,
    IChecksumVerifier checksumVerifier,
    ISdkEnvironmentService environmentService,
    IHttpClientFactory httpClientFactory,
    JwmvPaths paths) : ISdkVersionManager
{
    private const int MaxChecksumDocumentBytes = 64 * 1024;

    private readonly IReadOnlyDictionary<string, ISdkCatalogProvider> _providers = catalogProviders
        .ToDictionary(provider => provider.Candidate.Name, StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<SdkCandidate>> ListCandidatesAsync(string? filter, CancellationToken cancellationToken)
    {
        var packages = await GetCatalogAsync(forceRefresh: false, cancellationToken);
        return _providers.Values
            .Select(provider =>
            {
                packages.TryGetValue(provider.Candidate.Name, out var candidatePackages);
                var latest = candidatePackages?
                    .OrderBy(package => package.Version, Comparer<string>.Create(GetVersionComparer(provider.Candidate.Name)))
                    .FirstOrDefault()?.Alias;
                return provider.Candidate with { LatestVersion = latest };
            })
            .Where(candidate => string.IsNullOrWhiteSpace(filter)
                || candidate.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || candidate.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<SdkPackage>> ListAvailableAsync(SdkCatalogQuery query, CancellationToken cancellationToken)
    {
        var packages = await GetCatalogAsync(query.ForceRefresh, cancellationToken);
        var candidateName = string.IsNullOrWhiteSpace(query.CandidateName) ? null : SdkIdentifier.NormalizeCandidateName(query.CandidateName);
        var selected = candidateName is null
            ? packages.Values.SelectMany(items => items)
            : packages.TryGetValue(candidateName, out var candidatePackages) ? candidatePackages : [];

        return selected
            .Where(package => string.IsNullOrWhiteSpace(query.VersionFilter) || Matches(package.CandidateName, package.Alias, query.VersionFilter))
            .OrderBy(package => package.CandidateName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(package => package.Alias, Comparer<string>.Create(SdkIdentifier.CompareVersionsDescending))
            .ToList();
    }

    public Task<IReadOnlyList<InstalledSdkVersion>> ListInstalledAsync(string? candidateName, CancellationToken cancellationToken) =>
        installationStore.GetInstalledVersionsAsync(candidateName, cancellationToken);

    public async Task<InstallSdkResult> InstallAsync(InstallSdkRequest request, IProgress<InstallProgressUpdate>? progress, CancellationToken cancellationToken)
    {
        var candidateName = ResolveCandidateName(request.CandidateName);
        var provider = GetProvider(candidateName);
        paths.EnsureCandidateCreated(candidateName);

        progress?.Report(new InstallProgressUpdate
        {
            Phase = InstallPhase.Resolving,
            Percentage = 2,
            Status = $"Resolving {candidateName} {request.Version}"
        });

        var exactInstalled = await installationStore.FindAsync(candidateName, request.Version, cancellationToken);
        if (exactInstalled is not null)
        {
            var alreadyInstalledResult = new InstallSdkResult
            {
                InstalledVersion = exactInstalled,
                AlreadyInstalled = true
            };

            if (request.SetAsDefault)
            {
                await SetDefaultInternalAsync(exactInstalled, cancellationToken);
                return alreadyInstalledResult with { DefaultWasUpdated = true };
            }

            return alreadyInstalledResult;
        }

        var packages = await GetCatalogAsync(request.ForceCatalogRefresh, cancellationToken);
        var package = ResolveBestPackage(candidateName, packages.TryGetValue(candidateName, out var candidatePackages) ? candidatePackages : [], request.Version);
        if (provider is ISdkPackageDetailsProvider detailsProvider)
        {
            package = await detailsProvider.GetPackageDetailsAsync(package, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(package.Checksum) && package.ChecksumUri is not null)
        {
            package = package with { Checksum = await ReadChecksumAsync(package.ChecksumUri, cancellationToken) };
        }

        ValidateCatalogPackage(candidateName, package);

        progress?.Report(new InstallProgressUpdate
        {
            Phase = InstallPhase.Resolving,
            Percentage = 8,
            Status = $"Selected {candidateName} {package.Alias}"
        });

        var archivePath = Path.Combine(paths.GetCandidateArchiveDirectory(candidateName), package.FileName);
        if (!File.Exists(archivePath))
        {
            progress?.Report(new InstallProgressUpdate
            {
                Phase = InstallPhase.Downloading,
                Percentage = 10,
                Status = $"Downloading {package.FileName}"
            });

            var downloadProgress = new Progress<ArchiveDownloadProgress>(update =>
            {
                var totalBytes = update.TotalBytes;
                var percentage = totalBytes is > 0
                    ? 10 + (update.BytesTransferred / (double)totalBytes.Value * 65d)
                    : 10;
                progress?.Report(new InstallProgressUpdate
                {
                    Phase = InstallPhase.Downloading,
                    Percentage = Math.Min(75, percentage),
                    Status = $"Downloading {package.FileName}"
                });
            });

            await archiveDownloader.DownloadAsync(package.DownloadUri, archivePath, downloadProgress, cancellationToken);
        }
        else
        {
            progress?.Report(new InstallProgressUpdate
            {
                Phase = InstallPhase.Downloading,
                Percentage = 75,
                Status = $"Using cached archive {package.FileName}"
            });
        }

        var integrityWarning = await VerifyArchiveIntegrityAsync(package, archivePath, request.AllowUnverified, cancellationToken);

        var extractionRoot = Path.Combine(paths.TempDirectory, Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(extractionRoot);

        try
        {
            progress?.Report(new InstallProgressUpdate
            {
                Phase = InstallPhase.Extracting,
                Percentage = 80,
                Status = $"Extracting {package.FileName}"
            });

            var extractionProgress = new Progress<ArchiveExtractionProgress>(update =>
            {
                var fraction = update.TotalEntries > 0
                    ? update.EntriesProcessed / (double)update.TotalEntries
                    : 0d;
                progress?.Report(new InstallProgressUpdate
                {
                    Phase = InstallPhase.Extracting,
                    Percentage = 80 + (fraction * 15d),
                    Status = $"Extracting {package.FileName}"
                });
            });

            await archiveExtractor.ExtractZipAsync(archivePath, extractionRoot, extractionProgress, cancellationToken);
            var home = FindSdkHome(extractionRoot, provider.Candidate);
            var finalDirectory = paths.GetCandidateVersionDirectory(candidateName, package.Alias);
            EnsureManagedInstallDirectory(candidateName, finalDirectory);

            progress?.Report(new InstallProgressUpdate
            {
                Phase = InstallPhase.Finalizing,
                Percentage = 96,
                Status = $"Finalizing {candidateName} {package.Alias}"
            });

            if (Directory.Exists(finalDirectory))
            {
                Directory.Delete(finalDirectory, recursive: true);
            }

            Directory.Move(home, finalDirectory);
            var binDirectory = Path.Combine(finalDirectory, provider.Candidate.BinRelativePath);
            var installed = new InstalledSdkVersion
            {
                CandidateName = candidateName,
                Version = package.Version,
                Alias = package.Alias,
                DisplayName = package.DisplayName,
                Distribution = package.Distribution,
                DistributionAlias = package.DistributionAlias,
                InstallDirectory = finalDirectory,
                HomeDirectory = finalDirectory,
                BinDirectory = binDirectory,
                HomeEnvironmentVariable = provider.Candidate.HomeEnvironmentVariable,
                PrimaryExecutableName = provider.Candidate.PrimaryExecutableName,
                ArchiveType = package.ArchiveType,
                PackageFileName = package.FileName,
                SourcePackageId = package.Id,
                Checksum = package.Checksum,
                ChecksumType = package.ChecksumType,
                InstalledAtUtc = clock.UtcNow
            };

            await installationStore.SaveAsync(installed, cancellationToken);
            var defaultWasUpdated = false;
            if (request.SetAsDefault)
            {
                await SetDefaultInternalAsync(installed, cancellationToken);
                defaultWasUpdated = true;
            }

            progress?.Report(new InstallProgressUpdate
            {
                Phase = InstallPhase.Completed,
                Percentage = 100,
                Status = $"Installed {candidateName} {installed.Alias}"
            });

            return new InstallSdkResult
            {
                InstalledVersion = installed,
                AlreadyInstalled = false,
                DefaultWasUpdated = defaultWasUpdated,
                IntegrityWarning = integrityWarning
            };
        }
        finally
        {
            if (Directory.Exists(extractionRoot))
            {
                Directory.Delete(extractionRoot, recursive: true);
            }
        }
    }

    public async Task<IReadOnlyList<InstallSdkResult>> ImportLocalAsync(ImportLocalSdkRequest request, CancellationToken cancellationToken)
    {
        var candidateName = ResolveCandidateName(request.CandidateName);
        var provider = GetProvider(candidateName);
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            throw new JwmvException("A local installation path is required.");
        }

        var resolvedPath = Path.GetFullPath(request.Path.Trim());
        if (!Directory.Exists(resolvedPath))
        {
            throw new JwmvException($"The path '{resolvedPath}' does not exist.");
        }

        var homes = DiscoverSdkHomes(resolvedPath, provider.Candidate);
        if (homes.Count == 0)
        {
            throw new JwmvException($"No {provider.Candidate.DisplayName} home with {provider.Candidate.PrimaryExecutableName} was found under '{resolvedPath}'.");
        }

        if (homes.Count > 1 && !request.ImportAll)
        {
            throw new JwmvException(
                $"Multiple {provider.Candidate.DisplayName} homes were found under '{resolvedPath}'. Import a specific directory, or pass --all to import all of them: {string.Join(", ", homes)}");
        }

        if (homes.Count > 1 && !string.IsNullOrWhiteSpace(request.Alias))
        {
            throw new JwmvException("A custom --alias cannot be used with --all. Import one directory at a time to assign a custom alias.");
        }

        var results = new List<InstallSdkResult>();
        foreach (var home in homes.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            results.Add(await ImportSingleLocalHomeAsync(
                candidateName,
                provider.Candidate,
                home,
                request.Alias,
                setAsDefault: false,
                request.AllowJre,
                cancellationToken));
        }

        if (request.SetAsDefault && results.Count > 0)
        {
            var preferred = results
                .Select(result => result.InstalledVersion)
                .OrderBy(item => item.Version, Comparer<string>.Create(GetVersionComparer(candidateName)))
                .First();
            await SetDefaultInternalAsync(preferred, cancellationToken);
            results = results
                .Select(result => string.Equals(result.InstalledVersion.Alias, preferred.Alias, StringComparison.OrdinalIgnoreCase)
                    ? result with { DefaultWasUpdated = true }
                    : result)
                .ToList();
        }
        else if (results.Count > 0)
        {
            // If the machine already points JAVA_HOME / *_HOME at an imported home, adopt it as
            // jwmv's configured default so the Installed list shows the badge without a manual re-set.
            results = (await TryAdoptEnvironmentDefaultAsync(candidateName, provider.Candidate, results, cancellationToken)).ToList();
        }

        return results;
    }

    private async Task<InstallSdkResult> ImportSingleLocalHomeAsync(
        string candidateName,
        SdkCandidate candidate,
        string home,
        string? requestedAlias,
        bool setAsDefault,
        bool allowJre,
        CancellationToken cancellationToken)
    {
        var binDirectory = Path.Combine(home, candidate.BinRelativePath);
        var primaryExecutable = Path.Combine(binDirectory, candidate.PrimaryExecutableName);
        if (!File.Exists(primaryExecutable))
        {
            throw new JwmvException($"No {candidate.PrimaryExecutableName} was found under '{home}'.");
        }

        string? integrityWarning = null;
        if (string.Equals(candidateName, JwmvConstants.CandidateName, StringComparison.OrdinalIgnoreCase))
        {
            var javacPath = Path.Combine(binDirectory, OperatingSystem.IsWindows() ? "javac.exe" : "javac");
            if (!File.Exists(javacPath))
            {
                if (!allowJre)
                {
                    throw new JwmvException(
                        $"'{home}' looks like a JRE (java found, javac missing). Install/import a JDK, or pass --allow-jre to register a runtime-only Java.");
                }

                integrityWarning = $"Imported '{home}' as a JRE-only runtime (javac was not found).";
            }
        }

        var version = ReadLocalVersion(home) ?? Path.GetFileName(home.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var alias = string.IsNullOrWhiteSpace(requestedAlias)
            ? BuildLocalAlias(version)
            : requestedAlias.Trim();
        if (!SdkIdentifier.IsSafeAlias(alias))
        {
            throw new JwmvException($"'{alias}' is not a valid SDK version alias.");
        }

        var existing = await installationStore.FindAsync(candidateName, alias, cancellationToken);
        if (existing is not null)
        {
            if (string.Equals(existing.HomeDirectory, home, StringComparison.OrdinalIgnoreCase))
            {
                if (setAsDefault)
                {
                    await SetDefaultInternalAsync(existing, cancellationToken);
                    return new InstallSdkResult
                    {
                        InstalledVersion = existing,
                        AlreadyInstalled = true,
                        DefaultWasUpdated = true,
                        IntegrityWarning = integrityWarning
                    };
                }

                return new InstallSdkResult
                {
                    InstalledVersion = existing,
                    AlreadyInstalled = true,
                    IntegrityWarning = integrityWarning
                };
            }

            throw new JwmvException($"Alias '{alias}' is already registered for {existing.HomeDirectory}.");
        }

        var installed = new InstalledSdkVersion
        {
            CandidateName = candidateName,
            Version = version,
            Alias = alias,
            DisplayName = $"{candidate.DisplayName} {version} (local)",
            Distribution = "Local",
            DistributionAlias = "local",
            InstallDirectory = home,
            HomeDirectory = home,
            BinDirectory = binDirectory,
            HomeEnvironmentVariable = candidate.HomeEnvironmentVariable,
            PrimaryExecutableName = candidate.PrimaryExecutableName,
            ArchiveType = "external",
            PackageFileName = string.Empty,
            SourcePackageId = "local-import",
            InstalledAtUtc = clock.UtcNow,
            IsExternal = true
        };

        await installationStore.SaveAsync(installed, cancellationToken);
        var defaultWasUpdated = false;
        if (setAsDefault)
        {
            await SetDefaultInternalAsync(installed, cancellationToken);
            defaultWasUpdated = true;
        }

        return new InstallSdkResult
        {
            InstalledVersion = installed,
            AlreadyInstalled = false,
            DefaultWasUpdated = defaultWasUpdated,
            IntegrityWarning = integrityWarning
        };
    }

    public async Task UninstallAsync(string candidateName, string version, CancellationToken cancellationToken)
    {
        var normalizedCandidate = ResolveCandidateName(candidateName);
        var installed = await ResolveInstalledAsync(normalizedCandidate, version, cancellationToken)
            ?? throw new JavaNotInstalledException($"{normalizedCandidate} {version}");

        if (!installed.IsExternal && Directory.Exists(installed.InstallDirectory))
        {
            EnsureManagedInstallDirectory(normalizedCandidate, installed.InstallDirectory);
        }

        var config = await configStore.LoadAsync(cancellationToken);
        if (config.DefaultVersions.TryGetValue(normalizedCandidate, out var defaultVersion) &&
            string.Equals(defaultVersion, installed.Alias, StringComparison.OrdinalIgnoreCase))
        {
            config.DefaultVersions.Remove(normalizedCandidate);
            await configStore.SaveAsync(config, cancellationToken);
            await environmentService.ClearDefaultAsync(normalizedCandidate, cancellationToken);
        }

        if (!installed.IsExternal && Directory.Exists(installed.InstallDirectory))
        {
            Directory.Delete(installed.InstallDirectory, recursive: true);
        }

        if (!installed.IsExternal && SdkIdentifier.IsSafeFileName(installed.PackageFileName))
        {
            var archivePath = Path.Combine(paths.GetCandidateArchiveDirectory(normalizedCandidate), installed.PackageFileName);
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }
        }

        await installationStore.DeleteAsync(normalizedCandidate, installed.Alias, cancellationToken);
    }

    public async Task<InstalledSdkVersion> SetDefaultAsync(string candidateName, string version, CancellationToken cancellationToken)
    {
        var normalizedCandidate = ResolveCandidateName(candidateName);
        var installed = await ResolveInstalledAsync(normalizedCandidate, version, cancellationToken)
            ?? throw new JavaNotInstalledException($"{normalizedCandidate} {version}");

        await SetDefaultInternalAsync(installed, cancellationToken);
        return installed;
    }

    public async Task<ActiveSdkSelection> ResolveCurrentAsync(string candidateName, string? workingDirectory, CancellationToken cancellationToken)
    {
        var normalizedCandidate = ResolveCandidateName(candidateName);
        var sessionAlias = appContext.GetEnvironmentVariable(SdkEnvironmentService.GetActiveVariable(normalizedCandidate, "VERSION"));
        if (!string.IsNullOrWhiteSpace(sessionAlias))
        {
            var installed = await installationStore.FindAsync(normalizedCandidate, sessionAlias, cancellationToken);
            if (installed is not null)
            {
                var source = Enum.TryParse<JavaActivationSource>(appContext.GetEnvironmentVariable(SdkEnvironmentService.GetActiveVariable(normalizedCandidate, "SOURCE")), true, out var parsed)
                    ? parsed
                    : JavaActivationSource.Session;
                return ToActiveSelection(installed, source);
            }
        }

        var projectConfig = await FindProjectConfigurationAsync(workingDirectory, cancellationToken);
        var projectVersion = projectConfig?.GetVersion(normalizedCandidate);
        if (!string.IsNullOrWhiteSpace(projectVersion))
        {
            var installed = await ResolveInstalledAsync(normalizedCandidate, projectVersion, cancellationToken);
            if (installed is not null)
            {
                return ToActiveSelection(installed, JavaActivationSource.Project) with { ProjectFilePath = projectConfig!.FilePath };
            }

            return new ActiveSdkSelection
            {
                CandidateName = normalizedCandidate,
                Version = projectVersion,
                Alias = projectVersion,
                Source = JavaActivationSource.Project,
                ProjectFilePath = projectConfig!.FilePath
            };
        }

        var config = await configStore.LoadAsync(cancellationToken);
        var defaultAlias = GetConfiguredDefault(config, normalizedCandidate);
        if (!string.IsNullOrWhiteSpace(defaultAlias))
        {
            var installed = await installationStore.FindAsync(normalizedCandidate, defaultAlias, cancellationToken);
            if (installed is not null)
            {
                return ToActiveSelection(installed, JavaActivationSource.Default);
            }
        }

        var provider = GetProvider(normalizedCandidate);
        var environmentHome = ReadHomeFromEnvironment(provider.Candidate.HomeEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentHome))
        {
            var installedByHome = await FindInstalledByHomeAsync(normalizedCandidate, environmentHome, cancellationToken);
            if (installedByHome is not null)
            {
                return ToActiveSelection(installedByHome, JavaActivationSource.Environment);
            }

            var binDirectory = Path.Combine(environmentHome, provider.Candidate.BinRelativePath);
            var label = Path.GetFileName(environmentHome.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return new ActiveSdkSelection
            {
                CandidateName = normalizedCandidate,
                Version = string.IsNullOrWhiteSpace(label) ? environmentHome : label,
                Alias = string.IsNullOrWhiteSpace(label) ? environmentHome : label,
                HomeDirectory = environmentHome,
                BinDirectory = Directory.Exists(binDirectory) ? binDirectory : environmentHome,
                Source = JavaActivationSource.Environment
            };
        }

        return new ActiveSdkSelection { CandidateName = normalizedCandidate, Source = JavaActivationSource.None };
    }

    public async Task<IReadOnlyList<ActiveSdkSelection>> ResolveAllCurrentAsync(string? workingDirectory, CancellationToken cancellationToken)
    {
        var result = new List<ActiveSdkSelection>();
        foreach (var candidate in _providers.Keys.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            result.Add(await ResolveCurrentAsync(candidate, workingDirectory, cancellationToken));
        }

        return result;
    }

    public async Task<string?> GetHomeAsync(string candidateName, string? version, CancellationToken cancellationToken)
    {
        var normalizedCandidate = ResolveCandidateName(candidateName);
        if (string.IsNullOrWhiteSpace(version))
        {
            return (await ResolveCurrentAsync(normalizedCandidate, appContext.WorkingDirectory, cancellationToken)).HomeDirectory;
        }

        var installed = await ResolveInstalledAsync(normalizedCandidate, version, cancellationToken)
            ?? throw new JavaNotInstalledException($"{normalizedCandidate} {version}");
        return installed.HomeDirectory;
    }

    public async Task<string> BuildUseShellScriptAsync(string candidateName, string version, ShellKind shellKind, CancellationToken cancellationToken)
    {
        if (shellKind != ShellKind.PowerShell)
        {
            throw new JwmvException("Only PowerShell shell integration is implemented.");
        }

        var normalizedCandidate = ResolveCandidateName(candidateName);
        var installed = await ResolveInstalledAsync(normalizedCandidate, version, cancellationToken)
            ?? throw new JavaNotInstalledException($"{normalizedCandidate} {version}");

        return BuildActivationScript(installed, JavaActivationSource.Session, emitConfirmationMessage: true);
    }

    public async Task<string> BuildEnvShellScriptAsync(string? workingDirectory, ShellKind shellKind, CancellationToken cancellationToken)
    {
        if (shellKind != ShellKind.PowerShell)
        {
            throw new JwmvException("Only PowerShell shell integration is implemented.");
        }

        var projectConfig = await FindProjectConfigurationAsync(workingDirectory, cancellationToken);
        var scripts = new List<string>();
        foreach (var candidate in _providers.Keys.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            var projectVersion = projectConfig?.GetVersion(candidate);
            if (!string.IsNullOrWhiteSpace(projectVersion))
            {
                var installed = await ResolveInstalledAsync(candidate, projectVersion, cancellationToken)
                    ?? throw new JavaNotInstalledException($"{candidate} {projectVersion}");
                scripts.Add(BuildActivationScript(installed, JavaActivationSource.Project));
                continue;
            }

            var currentSource = appContext.GetEnvironmentVariable(SdkEnvironmentService.GetActiveVariable(candidate, "SOURCE"));
            if (string.Equals(currentSource, nameof(JavaActivationSource.Project), StringComparison.OrdinalIgnoreCase))
            {
                var config = await configStore.LoadAsync(cancellationToken);
                var defaultAlias = GetConfiguredDefault(config, candidate);
                if (!string.IsNullOrWhiteSpace(defaultAlias))
                {
                    var installedDefault = await ResolveInstalledAsync(candidate, defaultAlias, cancellationToken);
                    if (installedDefault is not null)
                    {
                        scripts.Add(BuildActivationScript(installedDefault, JavaActivationSource.Default));
                        continue;
                    }
                }

                scripts.Add(BuildClearScript(candidate));
            }
        }

        return string.Join(Environment.NewLine, scripts);
    }

    public Task<string> BuildShellInitScriptAsync(ShellKind shellKind, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (shellKind != ShellKind.PowerShell)
        {
            throw new JwmvException("Only PowerShell shell integration is implemented.");
        }

        var executable = PowerShellLiteral.Escape(JwmvCliLocator.ResolveCliExecutablePath(appContext));
        var script = $$"""
$script:jwmvExe = '{{executable}}'
$env:{{JwmvConstants.ShellIntegrationVariable}} = '1'

function global:jwmv {
    param([Parameter(ValueFromRemainingArguments = $true)] [string[]]$JwmvArgs)

    $shouldEvaluate = $false
    if ($JwmvArgs.Length -gt 0 -and $JwmvArgs[0].ToLowerInvariant() -eq 'use') {
        $shouldEvaluate = $true
    }
    if ($JwmvArgs.Length -gt 0 -and $JwmvArgs[0].ToLowerInvariant() -eq 'env') {
        $envAction = if ($JwmvArgs.Length -gt 1) { $JwmvArgs[1].ToLowerInvariant() } else { '' }
        $shouldEvaluate = -not (@('install', 'init') -contains $envAction)
    }

    if ($shouldEvaluate) {
        $result = & $script:jwmvExe @JwmvArgs --shell powershell
        if ($LASTEXITCODE -eq 0 -and $result) {
            Invoke-Expression ($result -join [Environment]::NewLine)
        }
        return
    }

    & $script:jwmvExe @JwmvArgs
}

if (-not (Test-Path function:\global:__jwmv_original_prompt)) {
    $function:global:__jwmv_original_prompt = $function:prompt
}

function global:prompt {
    $result = & $script:jwmvExe env --shell powershell --cwd (Get-Location).Path
    if ($LASTEXITCODE -eq 0 -and $result) {
        Invoke-Expression ($result -join [Environment]::NewLine)
    }

    & $function:global:__jwmv_original_prompt
}
""";

        return Task.FromResult(script);
    }

    public async Task<ProjectSdkConfiguration?> FindProjectConfigurationAsync(string? workingDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var startDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? appContext.WorkingDirectory : workingDirectory;
        var current = new DirectoryInfo(startDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, JwmvConstants.ProjectFileName);
            if (File.Exists(candidate))
            {
                var versions = await ParseProjectConfigurationAsync(candidate, cancellationToken);
                if (versions.Count == 0)
                {
                    throw new ProjectConfigurationException($"The project file '{candidate}' does not contain valid SDK entries.");
                }

                return new ProjectSdkConfiguration
                {
                    Versions = versions,
                    FilePath = candidate
                };
            }

            current = current.Parent;
        }

        return null;
    }

    public async Task<IReadOnlyList<InstallSdkResult>> InstallProjectMissingAsync(string? workingDirectory, bool forceCatalogRefresh, IProgress<InstallProgressUpdate>? progress, CancellationToken cancellationToken)
    {
        var projectConfig = await FindProjectConfigurationAsync(workingDirectory, cancellationToken)
            ?? throw new ProjectConfigurationException("No .jwmvrc file found in the current directory tree.");

        var results = new List<InstallSdkResult>();
        foreach (var item in projectConfig.Versions)
        {
            var installed = await ResolveInstalledAsync(item.Key, item.Value, cancellationToken);
            if (installed is not null)
            {
                continue;
            }

            results.Add(await InstallAsync(new InstallSdkRequest
            {
                CandidateName = item.Key,
                Version = item.Value,
                ForceCatalogRefresh = forceCatalogRefresh
            }, progress, cancellationToken));
        }

        return results;
    }

    public async Task RefreshCatalogAsync(CancellationToken cancellationToken)
    {
        var packages = new Dictionary<string, List<SdkPackage>>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in _providers.Values)
        {
            packages[provider.Candidate.Name] = (await provider.GetPackagesAsync(cancellationToken)).ToList();
        }

        await catalogCacheStore.SaveAsync(new SdkCatalogCache
        {
            RefreshedAtUtc = clock.UtcNow,
            PackagesByCandidate = packages
        }, cancellationToken);
    }

    public Task<AppConfig> GetConfigAsync(CancellationToken cancellationToken) =>
        configStore.LoadAsync(cancellationToken);

    private async Task<IReadOnlyDictionary<string, List<SdkPackage>>> GetCatalogAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        paths.EnsureCreated();
        var config = await configStore.LoadAsync(cancellationToken);
        var cache = await catalogCacheStore.LoadAsync(cancellationToken);
        var isFresh = cache is not null && (clock.UtcNow - cache.RefreshedAtUtc) < TimeSpan.FromHours(config.CatalogRefreshHours);
        if (!forceRefresh && isFresh)
        {
            return cache!.PackagesByCandidate;
        }

        await RefreshCatalogAsync(cancellationToken);
        return (await catalogCacheStore.LoadAsync(cancellationToken))!.PackagesByCandidate;
    }

    private async Task SetDefaultInternalAsync(InstalledSdkVersion installed, CancellationToken cancellationToken)
    {
        var config = await configStore.LoadAsync(cancellationToken);
        config.DefaultVersions[installed.CandidateName] = installed.Alias;
        var updated = string.Equals(installed.CandidateName, JwmvConstants.CandidateName, StringComparison.OrdinalIgnoreCase)
            ? config with { DefaultJavaAlias = installed.Alias }
            : config;
        await configStore.SaveAsync(updated, cancellationToken);
        await environmentService.ApplyDefaultAsync(installed, cancellationToken);
    }

    private async Task<InstalledSdkVersion?> ResolveInstalledAsync(string candidateName, string version, CancellationToken cancellationToken) =>
        await installationStore.FindAsync(candidateName, version, cancellationToken);

    private async Task<string?> VerifyArchiveIntegrityAsync(SdkPackage package, string archivePath, bool allowUnverified, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(package.Checksum) || string.IsNullOrWhiteSpace(package.ChecksumType))
        {
            if (!allowUnverified)
            {
                TryDeleteFile(archivePath);
                throw new IntegrityVerificationException(
                    $"The catalog does not publish a checksum for {package.FileName}, so its integrity cannot be verified. " +
                    "Re-run with --allow-unverified to install it anyway.");
            }

            return $"{package.FileName} was installed unverified: the catalog does not publish a checksum for it.";
        }

        ChecksumVerificationResult checksum;
        try
        {
            checksum = await checksumVerifier.VerifyAsync(archivePath, package.Checksum, package.ChecksumType, cancellationToken);
        }
        catch (IntegrityVerificationException)
        {
            TryDeleteFile(archivePath);
            throw;
        }

        if (!checksum.IsValid)
        {
            TryDeleteFile(archivePath);
            throw new IntegrityVerificationException($"Checksum verification failed for {package.FileName}. Expected {checksum.Expected}, got {checksum.Actual}.");
        }

        return checksum.IsWeakAlgorithm
            ? $"{package.FileName} was verified with a weak checksum algorithm ({checksum.Algorithm}); the source catalog does not publish a stronger hash."
            : null;
    }

    private static void ValidateCatalogPackage(string candidateName, SdkPackage package)
    {
        if (!SdkIdentifier.IsSafeFileName(package.FileName))
        {
            throw new JwmvException($"The {candidateName} catalog returned an unsafe archive file name '{package.FileName}'. Refusing to install it.");
        }

        if (!SdkIdentifier.IsSafeAlias(package.Alias))
        {
            throw new JwmvException($"The {candidateName} catalog returned an unsafe version alias '{package.Alias}'. Refusing to install it.");
        }

        if (!string.Equals(package.DownloadUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new JwmvException($"The {candidateName} catalog returned a non-HTTPS download URL '{package.DownloadUri}'. Refusing to install it.");
        }
    }

    private void EnsureManagedInstallDirectory(string candidateName, string directory)
    {
        var candidateRoot = paths.GetCandidateDirectory(candidateName);
        if (!PathTools.IsStrictlyWithinDirectory(candidateRoot, directory))
        {
            throw new JwmvException($"Refusing to modify '{directory}' because it is outside the jwmv directory for {candidateName} ({candidateRoot}).");
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // The archive is rejected either way; a leftover file only wastes disk space.
        }
        catch (UnauthorizedAccessException)
        {
            // The archive is rejected either way; a leftover file only wastes disk space.
        }
    }

    private static SdkPackage ResolveBestPackage(string candidateName, IReadOnlyList<SdkPackage> packages, string version)
    {
        var package = packages
            .Where(item => Matches(candidateName, item.Alias, version))
            .OrderBy(item => item.Version, Comparer<string>.Create(GetVersionComparer(candidateName)))
            .FirstOrDefault();

        return package ?? throw new JavaPackageNotFoundException($"{candidateName} {version}");
    }

    private static bool Matches(string candidateName, string alias, string version) =>
        string.Equals(candidateName, JwmvConstants.CandidateName, StringComparison.OrdinalIgnoreCase)
            ? JavaIdentifier.Matches(alias, version)
            : SdkIdentifier.Matches(alias, version);

    private string ResolveCandidateName(string candidateName)
    {
        var normalized = SdkIdentifier.NormalizeCandidateName(candidateName);
        if (!_providers.ContainsKey(normalized))
        {
            throw new JwmvException($"'{candidateName}' is not a supported SDK candidate.");
        }

        return normalized;
    }

    private ISdkCatalogProvider GetProvider(string candidateName) => _providers[candidateName];

    private static Comparison<string?> GetVersionComparer(string candidateName) =>
        string.Equals(candidateName, JwmvConstants.CandidateName, StringComparison.OrdinalIgnoreCase)
            ? JavaIdentifier.CompareAliasesDescending
            : SdkIdentifier.CompareVersionsDescending;

    private static string FindSdkHome(string extractionRoot, SdkCandidate candidate)
    {
        if (HasExecutable(extractionRoot, candidate))
        {
            return extractionRoot;
        }

        var candidates = Directory.EnumerateDirectories(extractionRoot, "*", SearchOption.AllDirectories)
            .Where(path => HasExecutable(path, candidate))
            .OrderBy(path => path.Length)
            .ToList();

        return candidates.FirstOrDefault()
            ?? throw new InvalidOperationException($"The downloaded archive was extracted successfully, but no {candidate.DisplayName} home could be discovered.");
    }

    private static bool HasExecutable(string directoryPath, SdkCandidate candidate) =>
        File.Exists(Path.Combine(directoryPath, candidate.BinRelativePath, candidate.PrimaryExecutableName));

    private static IReadOnlyList<string> DiscoverSdkHomes(string path, SdkCandidate candidate)
    {
        var normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(Path.GetFileName(normalized), candidate.BinRelativePath, StringComparison.OrdinalIgnoreCase)
            && Path.GetDirectoryName(normalized) is { } parent
            && HasExecutable(parent, candidate))
        {
            return [parent];
        }

        if (HasExecutable(normalized, candidate))
        {
            return [normalized];
        }

        return Directory.EnumerateDirectories(normalized, "*", SearchOption.TopDirectoryOnly)
            .Where(directory => HasExecutable(directory, candidate))
            .OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? ReadLocalVersion(string homeDirectory)
    {
        var releaseFile = Path.Combine(homeDirectory, "release");
        if (!File.Exists(releaseFile))
        {
            return null;
        }

        string? javaVersion = null;
        string? implementorVersion = null;
        foreach (var line in File.ReadLines(releaseFile))
        {
            if (javaVersion is null && line.StartsWith("JAVA_VERSION=", StringComparison.OrdinalIgnoreCase))
            {
                javaVersion = line["JAVA_VERSION=".Length..].Trim().Trim('"');
            }
            else if (implementorVersion is null && line.StartsWith("IMPLEMENTOR_VERSION=", StringComparison.OrdinalIgnoreCase))
            {
                implementorVersion = line["IMPLEMENTOR_VERSION=".Length..].Trim().Trim('"');
            }
        }

        return javaVersion ?? implementorVersion;
    }

    private static string BuildLocalAlias(string version)
    {
        var sanitized = new string(version
            .Select(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_' or '+'
                ? character
                : '-')
            .ToArray())
            .Trim('-', '.', '_', '+');

        if (string.IsNullOrWhiteSpace(sanitized) || !char.IsAsciiLetterOrDigit(sanitized[0]))
        {
            sanitized = "local";
        }

        var alias = sanitized.EndsWith("-local", StringComparison.OrdinalIgnoreCase)
            ? sanitized
            : $"{sanitized}-local";

        return SdkIdentifier.IsSafeAlias(alias) ? alias : "local-import";
    }

    private static ActiveSdkSelection ToActiveSelection(InstalledSdkVersion installed, JavaActivationSource source) =>
        new()
        {
            CandidateName = installed.CandidateName,
            Version = installed.Version,
            Alias = installed.Alias,
            HomeDirectory = installed.HomeDirectory,
            BinDirectory = installed.BinDirectory,
            Source = source
        };

    private async Task<IReadOnlyList<InstallSdkResult>> TryAdoptEnvironmentDefaultAsync(
        string candidateName,
        SdkCandidate candidate,
        IReadOnlyList<InstallSdkResult> results,
        CancellationToken cancellationToken)
    {
        var environmentHome = ReadHomeFromEnvironment(candidate.HomeEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(environmentHome))
        {
            return results;
        }

        var normalizedEnv = NormalizePath(environmentHome);
        var match = results.FirstOrDefault(result =>
            string.Equals(NormalizePath(result.InstalledVersion.HomeDirectory), normalizedEnv, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return results;
        }

        var config = await configStore.LoadAsync(cancellationToken);
        var configuredAlias = GetConfiguredDefault(config, candidateName);
        if (string.Equals(configuredAlias, match.InstalledVersion.Alias, StringComparison.OrdinalIgnoreCase))
        {
            return results;
        }

        await SetDefaultInternalAsync(match.InstalledVersion, cancellationToken);
        return results
            .Select(result => string.Equals(result.InstalledVersion.Alias, match.InstalledVersion.Alias, StringComparison.OrdinalIgnoreCase)
                ? result with { DefaultWasUpdated = true }
                : result)
            .ToList();
    }

    private string? ReadHomeFromEnvironment(string homeVariableName)
    {
        if (string.IsNullOrWhiteSpace(homeVariableName))
        {
            return null;
        }

        var processValue = appContext.GetEnvironmentVariable(homeVariableName);
        if (!string.IsNullOrWhiteSpace(processValue))
        {
            return NormalizeExistingDirectory(processValue);
        }

        // GUI / CLI may have been started before the user edited environment variables;
        // also read the persisted User/Machine scope so Overview / Import can see JAVA_HOME / MAVEN_HOME / etc.
        if (OperatingSystem.IsWindows())
        {
            var userValue = Environment.GetEnvironmentVariable(homeVariableName, EnvironmentVariableTarget.User);
            if (!string.IsNullOrWhiteSpace(userValue))
            {
                return NormalizeExistingDirectory(userValue);
            }

            var machineValue = Environment.GetEnvironmentVariable(homeVariableName, EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrWhiteSpace(machineValue))
            {
                return NormalizeExistingDirectory(machineValue);
            }
        }

        return null;
    }

    private async Task<InstalledSdkVersion?> FindInstalledByHomeAsync(
        string candidateName,
        string homeDirectory,
        CancellationToken cancellationToken)
    {
        var installed = await installationStore.GetInstalledVersionsAsync(candidateName, cancellationToken);
        var normalizedHome = NormalizePath(homeDirectory);
        return installed.FirstOrDefault(item =>
            string.Equals(NormalizePath(item.HomeDirectory), normalizedHome, StringComparison.OrdinalIgnoreCase));
    }

    private static string? NormalizeExistingDirectory(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path.Trim());
            return Directory.Exists(fullPath) ? fullPath : null;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    private string BuildActivationScript(InstalledSdkVersion installed, JavaActivationSource source, bool emitConfirmationMessage = false)
    {
        var candidate = ResolveCandidateName(installed.CandidateName);
        // The home variable name comes from the provider definition, never from the on-disk manifest,
        // because everything interpolated here as an identifier ends up evaluated by Invoke-Expression.
        var homeEnvironmentVariable = GetProvider(candidate).Candidate.HomeEnvironmentVariable;
        var previousBinVariable = SdkEnvironmentService.GetActiveVariable(candidate, "BIN");
        var versionVariable = SdkEnvironmentService.GetActiveVariable(candidate, "VERSION");
        var homeVariable = SdkEnvironmentService.GetActiveVariable(candidate, "HOME");
        var binVariable = SdkEnvironmentService.GetActiveVariable(candidate, "BIN");
        var sourceVariable = SdkEnvironmentService.GetActiveVariable(candidate, "SOURCE");
        EnsureSafeVariableNames(homeEnvironmentVariable, previousBinVariable, versionVariable, homeVariable, binVariable, sourceVariable);

        var home = PowerShellLiteral.Escape(installed.HomeDirectory);
        var bin = PowerShellLiteral.Escape(installed.BinDirectory);
        var alias = PowerShellLiteral.Escape(installed.Alias);
        var sourceText = PowerShellLiteral.Escape(source.ToString());
        var confirmation = emitConfirmationMessage
            ? $"Write-Host 'Activated {PowerShellLiteral.Escape(candidate)} {alias} for this session.' -ForegroundColor Green"
            : string.Empty;

        // Keep CRLF so the script pastes correctly into Windows Terminal / PowerShell.
        var script = $$"""
# jwmv session activation — run in PowerShell (not cmd.exe)
# Tip: in PowerShell you can also run:  iex (Get-Clipboard)
$__jwmvPreviousBin = $env:{{previousBinVariable}}
if ($__jwmvPreviousBin) {
    $env:Path = @($env:Path -split ';' | Where-Object { $_ -and $_ -ne $__jwmvPreviousBin }) -join ';'
}

$env:{{homeEnvironmentVariable}} = '{{home}}'
$env:{{versionVariable}} = '{{alias}}'
$env:{{homeVariable}} = '{{home}}'
$env:{{binVariable}} = '{{bin}}'
$env:{{sourceVariable}} = '{{sourceText}}'
$env:Path = '{{bin}};' + (@($env:Path -split ';' | Where-Object { $_ -and $_ -ne '{{bin}}' }) -join ';')
{{confirmation}}
""";
        return script.ReplaceLineEndings("\r\n").TrimEnd() + "\r\n";
    }

    private string BuildClearScript(string candidateName)
    {
        var candidate = ResolveCandidateName(candidateName);
        var previousBinVariable = SdkEnvironmentService.GetActiveVariable(candidate, "BIN");
        var versionVariable = SdkEnvironmentService.GetActiveVariable(candidate, "VERSION");
        var homeVariable = SdkEnvironmentService.GetActiveVariable(candidate, "HOME");
        var binVariable = SdkEnvironmentService.GetActiveVariable(candidate, "BIN");
        var sourceVariable = SdkEnvironmentService.GetActiveVariable(candidate, "SOURCE");
        var homeEnvironmentVariable = GetProvider(candidate).Candidate.HomeEnvironmentVariable;
        EnsureSafeVariableNames(homeEnvironmentVariable, previousBinVariable, versionVariable, homeVariable, binVariable, sourceVariable);

        return $$"""
$__jwmvPreviousBin = $env:{{previousBinVariable}}
if ($__jwmvPreviousBin) {
    $env:Path = @($env:Path -split ';' | Where-Object { $_ -and $_ -ne $__jwmvPreviousBin }) -join ';'
}

Remove-Item Env:\{{homeEnvironmentVariable}} -ErrorAction SilentlyContinue
Remove-Item Env:\{{versionVariable}} -ErrorAction SilentlyContinue
Remove-Item Env:\{{homeVariable}} -ErrorAction SilentlyContinue
Remove-Item Env:\{{binVariable}} -ErrorAction SilentlyContinue
Remove-Item Env:\{{sourceVariable}} -ErrorAction SilentlyContinue
""";
    }

    private static void EnsureSafeVariableNames(params string[] variableNames)
    {
        foreach (var variableName in variableNames)
        {
            if (!SdkIdentifier.IsSafeEnvironmentVariableName(variableName))
            {
                throw new JwmvException($"'{variableName}' is not a valid environment variable name; refusing to generate a shell script with it.");
            }
        }
    }

    private static async Task<Dictionary<string, string>> ParseProjectConfigurationAsync(string filePath, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var lineNumber = index + 1;
            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                if (string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                {
                    throw new ProjectConfigurationException(
                        $"The project file '{filePath}' has an incomplete entry on line {lineNumber}: expected '<candidate>=<version>'.");
                }

                var candidate = SdkIdentifier.NormalizeCandidateName(parts[0]);
                if (!SdkIdentifier.IsSafeCandidateName(candidate))
                {
                    throw new ProjectConfigurationException($"The project file '{filePath}' has an invalid candidate name on line {lineNumber}: '{parts[0]}'.");
                }

                result[candidate] = ValidateProjectVersion(filePath, lineNumber, parts[1]);
                continue;
            }

            result[JwmvConstants.CandidateName] = ValidateProjectVersion(filePath, lineNumber, line);
        }

        return result;
    }

    private static string ValidateProjectVersion(string filePath, int lineNumber, string value)
    {
        if (!SdkIdentifier.IsSafeAlias(value))
        {
            throw new ProjectConfigurationException(
                $"The project file '{filePath}' has an invalid version on line {lineNumber}. " +
                "Versions may only contain letters, digits, dots, dashes, underscores and plus signs.");
        }

        return value;
    }

    private async Task<string> ReadChecksumAsync(Uri checksumUri, CancellationToken cancellationToken)
    {
        if (!string.Equals(checksumUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new IntegrityVerificationException($"Checksum URL '{checksumUri}' must use HTTPS.");
        }

        using var client = httpClientFactory.CreateClient(ServiceCollectionExtensions.FoojayClientName);
        using var response = await client.GetAsync(checksumUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new IntegrityVerificationException($"Unable to download the checksum from '{checksumUri}' (HTTP {(int)response.StatusCode}).");
        }

        if (response.Content.Headers.ContentLength is > MaxChecksumDocumentBytes)
        {
            throw new IntegrityVerificationException($"The checksum document at '{checksumUri}' is unexpectedly large.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[MaxChecksumDocumentBytes + 1];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        if (total > MaxChecksumDocumentBytes)
        {
            throw new IntegrityVerificationException($"The checksum document at '{checksumUri}' is unexpectedly large.");
        }

        return Encoding.UTF8.GetString(buffer, 0, total).Trim();
    }

    private static string? GetConfiguredDefault(AppConfig config, string candidateName)
    {
        if (config.DefaultVersions.TryGetValue(candidateName, out var configured))
        {
            return configured;
        }

        return string.Equals(candidateName, JwmvConstants.CandidateName, StringComparison.OrdinalIgnoreCase)
            ? config.DefaultJavaAlias
            : null;
    }
}
