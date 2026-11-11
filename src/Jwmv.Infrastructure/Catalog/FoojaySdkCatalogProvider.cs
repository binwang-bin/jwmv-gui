using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Jwmv.Core;
using Jwmv.Core.Abstractions;
using Jwmv.Core.Models;
using Jwmv.Core.Utilities;

namespace Jwmv.Infrastructure.Catalog;

public sealed class FoojaySdkCatalogProvider(IHttpClientFactory httpClientFactory, IAppContext appContext) : ISdkCatalogProvider, ISdkPackageDetailsProvider
{
    public SdkCandidate Candidate { get; } = new()
    {
        Name = JwmvConstants.CandidateName,
        DisplayName = "Java",
        Description = "OpenJDK distributions from Foojay Disco API.",
        WebsiteUri = new Uri("https://foojay.io/"),
        HomeEnvironmentVariable = "JAVA_HOME",
        PrimaryExecutableName = OperatingSystem.IsWindows() ? "java.exe" : "java"
    };

    public async Task<IReadOnlyList<SdkPackage>> GetPackagesAsync(CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient(ServiceCollectionExtensions.FoojayClientName);
        using var response = await client.GetAsync(BuildPackagesUri(appContext), cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await System.Text.Json.JsonSerializer.DeserializeAsync<FoojayPackagesResponse>(stream, Storage.JsonFileHelper.SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Unable to parse the Foojay package response.");

        return payload.Result
            .Where(package => package.Links?.PackageDownloadRedirect is not null)
            .Select(MapPackage)
            .OrderBy(package => package.Alias, Comparer<string>.Create(JavaIdentifier.CompareAliasesDescending))
            .ToList();
    }

    public async Task<SdkPackage> GetPackageDetailsAsync(SdkPackage package, CancellationToken cancellationToken)
    {
        if (package.PackageInfoUri is null || !string.IsNullOrWhiteSpace(package.Checksum))
        {
            return package;
        }

        using var client = httpClientFactory.CreateClient(ServiceCollectionExtensions.FoojayClientName);
        using var response = await client.GetAsync(package.PackageInfoUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await System.Text.Json.JsonSerializer.DeserializeAsync<FoojayPackageInfoResponse>(stream, Storage.JsonFileHelper.SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Unable to parse the Foojay package info response.");

        var details = payload.Result.FirstOrDefault();
        if (details is null)
        {
            return package;
        }

        return package with
        {
            DownloadUri = string.IsNullOrWhiteSpace(details.DirectDownloadUri) ? package.DownloadUri : new Uri(details.DirectDownloadUri, UriKind.Absolute),
            Checksum = details.Checksum,
            ChecksumType = details.ChecksumType,
            ChecksumUri = string.IsNullOrWhiteSpace(details.ChecksumUri) ? package.ChecksumUri : new Uri(details.ChecksumUri, UriKind.Absolute)
        };
    }

    private static Uri BuildPackagesUri(IAppContext appContext)
    {
        var architecture = appContext.ProcessArchitecture switch
        {
            Architecture.Arm64 => "aarch64",
            Architecture.X86 => "x86",
            _ => "x64"
        };

        var query = new Dictionary<string, string?>
        {
            ["package_type"] = "jdk",
            ["operating_system"] = "windows",
            ["release_status"] = "ga",
            ["archive_type"] = "zip",
            ["directly_downloadable"] = "true",
            ["architecture"] = architecture
        };

        var queryString = string.Join("&", query.Select(pair => $"{WebUtility.UrlEncode(pair.Key)}={WebUtility.UrlEncode(pair.Value)}"));
        return new Uri($"https://api.foojay.io/disco/v3.0/packages?{queryString}");
    }

    private static SdkPackage MapPackage(FoojayPackage source)
    {
        var distribution = source.Distribution ?? throw new InvalidOperationException("Missing distribution in Foojay package.");
        var javaVersion = source.JavaVersion ?? throw new InvalidOperationException("Missing java_version in Foojay package.");
        var distributionAlias = DistributionAlias.ToAlias(distribution);
        var alias = JavaIdentifier.BuildAlias(javaVersion, distribution);
        return new SdkPackage
        {
            Id = source.Id ?? Guid.NewGuid().ToString("n"),
            CandidateName = JwmvConstants.CandidateName,
            Version = javaVersion,
            Alias = alias,
            DisplayName = alias,
            Distribution = distribution,
            DistributionAlias = distributionAlias,
            ArchiveType = source.ArchiveType ?? "zip",
            FileName = source.FileName ?? $"{javaVersion}-{distribution}.zip",
            DownloadUri = new Uri(source.Links!.PackageDownloadRedirect!, UriKind.Absolute),
            PackageInfoUri = source.Links.PackageInfoUri is null ? null : new Uri(source.Links.PackageInfoUri, UriKind.Absolute),
            ReleaseStatus = source.ReleaseStatus ?? "ga",
            SupportTerm = source.TermOfSupport ?? string.Empty,
            Size = source.Size
        };
    }

    private sealed class FoojayPackagesResponse
    {
        [JsonPropertyName("result")]
        public List<FoojayPackage> Result { get; init; } = [];
    }

    private sealed class FoojayPackage
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("archive_type")]
        public string? ArchiveType { get; init; }

        [JsonPropertyName("distribution")]
        public string? Distribution { get; init; }

        [JsonPropertyName("java_version")]
        public string? JavaVersion { get; init; }

        [JsonPropertyName("release_status")]
        public string? ReleaseStatus { get; init; }

        [JsonPropertyName("term_of_support")]
        public string? TermOfSupport { get; init; }

        [JsonPropertyName("filename")]
        public string? FileName { get; init; }

        [JsonPropertyName("size")]
        public long Size { get; init; }

        [JsonPropertyName("links")]
        public FoojayLinks? Links { get; init; }
    }

    private sealed class FoojayLinks
    {
        [JsonPropertyName("pkg_info_uri")]
        public string? PackageInfoUri { get; init; }

        [JsonPropertyName("pkg_download_redirect")]
        public string? PackageDownloadRedirect { get; init; }
    }

    private sealed class FoojayPackageInfoResponse
    {
        [JsonPropertyName("result")]
        public List<FoojayPackageInfo> Result { get; init; } = [];
    }

    private sealed class FoojayPackageInfo
    {
        [JsonPropertyName("direct_download_uri")]
        public string? DirectDownloadUri { get; init; }

        [JsonPropertyName("checksum_uri")]
        public string? ChecksumUri { get; init; }

        [JsonPropertyName("checksum")]
        public string? Checksum { get; init; }

        [JsonPropertyName("checksum_type")]
        public string? ChecksumType { get; init; }
    }
}
