using System.Text.Json;
using Jwmv.Core;
using Jwmv.Core.Abstractions;
using Jwmv.Core.Exceptions;
using Jwmv.Core.Models;
using Jwmv.Core.Utilities;

namespace Jwmv.Infrastructure.Storage;

public sealed class JsonSdkInstallationStore(JwmvPaths paths) : ISdkInstallationStore
{
    public async Task<IReadOnlyList<InstalledSdkVersion>> GetInstalledVersionsAsync(string? candidateName, CancellationToken cancellationToken)
    {
        paths.EnsureCreated();
        var result = new List<InstalledSdkVersion>();
        IEnumerable<string> roots;
        if (string.IsNullOrWhiteSpace(candidateName))
        {
            roots = Directory.EnumerateDirectories(Path.Combine(paths.VarDirectory, JwmvConstants.ManifestsDirectoryName), "*", SearchOption.TopDirectoryOnly);
        }
        else
        {
            var normalizedCandidate = SdkIdentifier.NormalizeCandidateName(candidateName);
            if (!SdkIdentifier.IsSafeCandidateName(normalizedCandidate))
            {
                return result;
            }

            var candidateDirectory = paths.GetCandidateManifestsDirectory(normalizedCandidate);
            roots = Directory.Exists(candidateDirectory) ? [candidateDirectory] : [];
        }

        foreach (var root in roots)
        {
            foreach (var file in Directory.EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly))
            {
                var item = await ReadInstalledAsync(file, cancellationToken);
                if (item is not null)
                {
                    result.Add(item);
                }
            }
        }

        return result;
    }

    public async Task<InstalledSdkVersion?> FindAsync(string candidateName, string alias, CancellationToken cancellationToken)
    {
        // Aliases reach this method from untrusted sources (.jwmvrc files, environment variables, CLI input).
        // Anything that is not a plain version alias is never turned into a file system path.
        if (string.IsNullOrWhiteSpace(candidateName) || !SdkIdentifier.IsSafeAlias(alias))
        {
            return null;
        }

        var normalizedCandidate = SdkIdentifier.NormalizeCandidateName(candidateName);
        if (!SdkIdentifier.IsSafeCandidateName(normalizedCandidate))
        {
            return null;
        }

        var exact = await ReadInstalledAsync(GetManifestPath(normalizedCandidate, alias), cancellationToken);
        if (exact is not null)
        {
            return exact;
        }

        var installed = await GetInstalledVersionsAsync(normalizedCandidate, cancellationToken);
        return installed
            .Where(item => SdkIdentifier.Matches(item.Alias, alias))
            .OrderBy(item => item.Version, Comparer<string>.Create(SdkIdentifier.CompareVersionsDescending))
            .FirstOrDefault();
    }

    public Task SaveAsync(InstalledSdkVersion installedVersion, CancellationToken cancellationToken)
    {
        var normalizedCandidate = SdkIdentifier.NormalizeCandidateName(installedVersion.CandidateName);
        var manifestPath = GetManifestPath(normalizedCandidate, installedVersion.Alias);
        paths.EnsureCandidateCreated(normalizedCandidate);
        return JsonFileHelper.WriteAsync(manifestPath, installedVersion, cancellationToken);
    }

    public Task DeleteAsync(string candidateName, string alias, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedCandidate = SdkIdentifier.NormalizeCandidateName(candidateName);
        var path = GetManifestPath(normalizedCandidate, alias);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetManifestPath(string candidateName, string alias)
    {
        if (!SdkIdentifier.IsSafeCandidateName(candidateName))
        {
            throw new JwmvException($"'{candidateName}' is not a valid SDK candidate name.");
        }

        if (!SdkIdentifier.IsSafeAlias(alias))
        {
            throw new JwmvException($"'{alias}' is not a valid SDK version alias.");
        }

        var directory = paths.GetCandidateManifestsDirectory(candidateName);
        var manifestPath = Path.Combine(directory, $"{alias}.json");
        if (!PathTools.IsStrictlyWithinDirectory(directory, manifestPath))
        {
            throw new JwmvException($"The manifest path for '{alias}' escapes the jwmv manifests directory.");
        }

        return manifestPath;
    }

    private static async Task<InstalledSdkVersion?> ReadInstalledAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var sdk = await JsonFileHelper.ReadAsync<InstalledSdkVersion>(filePath, cancellationToken);
            if (sdk is not null && !string.IsNullOrWhiteSpace(sdk.CandidateName) && !string.IsNullOrWhiteSpace(sdk.HomeDirectory))
            {
                return sdk;
            }
        }
        catch (JsonException)
        {
            // Fall through to legacy InstalledJavaVersion shape.
        }

        try
        {
            var legacy = await JsonFileHelper.ReadAsync<InstalledJavaVersion>(filePath, cancellationToken);
            return legacy is null ? null : InstalledVersionMapper.ToSdk(legacy);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
