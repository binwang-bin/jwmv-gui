using System.Text.Json;
using Jwmv.Core.Abstractions;
using Jwmv.Core.Exceptions;
using Jwmv.Core.Models;
using Jwmv.Core.Utilities;

namespace Jwmv.Infrastructure.Storage;

public sealed class JsonJavaInstallationStore(JwmvPaths paths) : IJavaInstallationStore
{
    public async Task<IReadOnlyList<InstalledJavaVersion>> GetInstalledVersionsAsync(CancellationToken cancellationToken)
    {
        paths.EnsureCreated();
        var result = new List<InstalledJavaVersion>();
        foreach (var file in Directory.EnumerateFiles(paths.ManifestsDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            var item = await ReadInstalledAsync(file, cancellationToken);
            if (item is not null)
            {
                result.Add(item);
            }
        }

        return result;
    }

    public async Task<InstalledJavaVersion?> FindByAliasAsync(string alias, CancellationToken cancellationToken)
    {
        if (!SdkIdentifier.IsSafeAlias(alias))
        {
            return null;
        }

        var filePath = GetManifestPath(alias);
        var exact = await ReadInstalledAsync(filePath, cancellationToken);
        if (exact is not null)
        {
            return exact;
        }

        var installed = await GetInstalledVersionsAsync(cancellationToken);
        return installed
            .Where(item => SdkIdentifier.Matches(item.Alias, alias) || SdkIdentifier.Matches(item.JavaVersion, alias))
            .OrderBy(item => item.JavaVersion, Comparer<string>.Create(SdkIdentifier.CompareVersionsDescending))
            .FirstOrDefault();
    }

    public Task SaveAsync(InstalledJavaVersion installedVersion, CancellationToken cancellationToken)
    {
        // Persist in the current SDK manifest shape so doctor / installed / import stay consistent.
        var manifestPath = GetManifestPath(installedVersion.Alias);
        paths.EnsureCreated();
        return JsonFileHelper.WriteAsync(manifestPath, InstalledVersionMapper.ToSdk(installedVersion), cancellationToken);
    }

    public Task DeleteAsync(string alias, CancellationToken cancellationToken)
    {
        var path = GetManifestPath(alias);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetManifestPath(string alias)
    {
        if (!SdkIdentifier.IsSafeAlias(alias))
        {
            throw new JwmvException($"'{alias}' is not a valid Java version alias.");
        }

        var manifestPath = Path.Combine(paths.ManifestsDirectory, $"{alias}.json");
        if (!PathTools.IsStrictlyWithinDirectory(paths.ManifestsDirectory, manifestPath))
        {
            throw new JwmvException($"The manifest path for '{alias}' escapes the jwmv manifests directory.");
        }

        return manifestPath;
    }

    private static async Task<InstalledJavaVersion?> ReadInstalledAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var sdk = await JsonFileHelper.ReadAsync<InstalledSdkVersion>(filePath, cancellationToken);
            if (sdk is not null
                && (string.IsNullOrWhiteSpace(sdk.CandidateName)
                    || string.Equals(sdk.CandidateName, "java", StringComparison.OrdinalIgnoreCase)))
            {
                return InstalledVersionMapper.ToJava(sdk);
            }
        }
        catch (JsonException)
        {
            // Fall through to legacy InstalledJavaVersion shape.
        }

        try
        {
            return await JsonFileHelper.ReadAsync<InstalledJavaVersion>(filePath, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
