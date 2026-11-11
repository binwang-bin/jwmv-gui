using Jwmv.Core.Abstractions;
using Jwmv.Core.Exceptions;
using Jwmv.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Jwmv.Cli.Commands;

public sealed class UpgradeCommand(ISdkVersionManager manager, IAnsiConsole console) : AsyncCommand<UpgradeCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[candidate-or-version]")]
        public string? CandidateOrVersion { get; init; }

        [CommandArgument(1, "[version]")]
        public string? Version { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var (candidateName, version) = CommandHelpers.ResolveCandidateAndVersion(settings.CandidateOrVersion, settings.Version);
        var installedVersions = await manager.ListInstalledAsync(CommandHelpers.IsKnownCandidate(settings.CandidateOrVersion) ? candidateName : null, cancellationToken);
        var targets = string.IsNullOrWhiteSpace(version)
            ? installedVersions
            : installedVersions.Where(item => item.Alias.StartsWith(version, StringComparison.OrdinalIgnoreCase)).ToList();

        if (targets.Count == 0)
        {
            throw new JavaNotInstalledException(settings.CandidateOrVersion ?? "installed");
        }

        CommandHelpers.WriteHeader(console, "upgrade");
        var config = await manager.GetConfigAsync(cancellationToken);
        var processedTracks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var installed in targets.OrderByDescending(item => item.Version, StringComparer.OrdinalIgnoreCase))
        {
            var track = string.Equals(installed.CandidateName, "java", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(installed.DistributionAlias)
                ? $"{GetMajorVersion(installed.Version)}-{installed.DistributionAlias}"
                : GetMajorVersion(installed.Version);
            if (!processedTracks.Add(track))
            {
                continue;
            }

            var result = await CommandHelpers.RunInstallProgressAsync(
                console,
                $"Upgrading {installed.CandidateName} {track}",
                progress => manager.InstallAsync(new InstallSdkRequest
                {
                    CandidateName = installed.CandidateName,
                    Version = track,
                    SetAsDefault = config.DefaultVersions.TryGetValue(installed.CandidateName, out var defaultAlias) &&
                        string.Equals(defaultAlias, installed.Alias, StringComparison.OrdinalIgnoreCase)
                }, progress, cancellationToken));

            if (result is null)
            {
                return -1;
            }

            if (result.AlreadyInstalled)
            {
                CommandHelpers.WriteInfo(console, $"{installed.CandidateName} {track} already points to the latest installed package ({result.InstalledVersion.Alias}).");
            }
            else
            {
                CommandHelpers.WriteSuccess(console, $"Upgraded {installed.CandidateName} {track} -> {result.InstalledVersion.Alias}");
            }
        }

        return 0;
    }

    private static string GetMajorVersion(string javaVersion)
    {
        var digits = new string(javaVersion.TakeWhile(character => char.IsDigit(character)).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? javaVersion : digits;
    }
}
