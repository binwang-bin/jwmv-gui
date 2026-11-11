using Jwmv.Core;
using Jwmv.Core.Abstractions;

namespace Jwmv.Infrastructure;

public sealed class JwmvPaths
{
    public JwmvPaths(IAppContext appContext)
    {
        RootDirectory = ResolveRootDirectory(appContext);
        CandidatesRootDirectory = Path.Combine(RootDirectory, JwmvConstants.CandidatesDirectoryName);
        JavaCandidatesDirectory = Path.Combine(CandidatesRootDirectory, JwmvConstants.CandidateName);
        ArchivesDirectory = Path.Combine(RootDirectory, JwmvConstants.ArchivesDirectoryName);
        TempDirectory = Path.Combine(RootDirectory, JwmvConstants.TempDirectoryName);
        VarDirectory = Path.Combine(RootDirectory, JwmvConstants.VarDirectoryName);
        ManifestsDirectory = Path.Combine(VarDirectory, JwmvConstants.ManifestsDirectoryName, JwmvConstants.CandidateName);
        ConfigFilePath = Path.Combine(RootDirectory, JwmvConstants.ConfigFileName);
        CatalogCacheFilePath = Path.Combine(VarDirectory, JwmvConstants.CatalogCacheFileName);
        SdkCatalogCacheFilePath = Path.Combine(VarDirectory, JwmvConstants.SdkCatalogCacheFileName);
    }

    public string RootDirectory { get; }
    public string CandidatesRootDirectory { get; }
    public string JavaCandidatesDirectory { get; }
    public string ArchivesDirectory { get; }
    public string TempDirectory { get; }
    public string VarDirectory { get; }
    public string ManifestsDirectory { get; }
    public string ConfigFilePath { get; }
    public string CatalogCacheFilePath { get; }
    public string SdkCatalogCacheFilePath { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(CandidatesRootDirectory);
        Directory.CreateDirectory(JavaCandidatesDirectory);
        Directory.CreateDirectory(ArchivesDirectory);
        Directory.CreateDirectory(TempDirectory);
        Directory.CreateDirectory(VarDirectory);
        Directory.CreateDirectory(ManifestsDirectory);
    }

    public string GetCandidateDirectory(string candidateName) =>
        Path.Combine(CandidatesRootDirectory, candidateName);

    public string GetCandidateVersionDirectory(string candidateName, string version) =>
        Path.Combine(GetCandidateDirectory(candidateName), version);

    public string GetCandidateArchiveDirectory(string candidateName) =>
        Path.Combine(ArchivesDirectory, candidateName);

    public string GetCandidateManifestsDirectory(string candidateName) =>
        Path.Combine(VarDirectory, JwmvConstants.ManifestsDirectoryName, candidateName);

    public void EnsureCandidateCreated(string candidateName)
    {
        EnsureCreated();
        Directory.CreateDirectory(GetCandidateDirectory(candidateName));
        Directory.CreateDirectory(GetCandidateArchiveDirectory(candidateName));
        Directory.CreateDirectory(GetCandidateManifestsDirectory(candidateName));
    }

    private static string ResolveRootDirectory(IAppContext appContext)
    {
        var configured = FirstNonEmpty(
            appContext.GetEnvironmentVariable(JwmvConstants.HomeEnvironmentVariable),
            appContext.GetEnvironmentVariable(JwmvConstants.HomeEnvironmentVariableAlternate));
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured.Trim());
        }

        var baseDirectory = appContext.ApplicationBaseDirectory;
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = appContext.UserProfileDirectory;
        }

        return Path.Combine(
            Path.GetFullPath(baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            ".jwmv");
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
