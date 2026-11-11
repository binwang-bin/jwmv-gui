using Jwmv.Core.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Jwmv.Cli.Commands;

public sealed class UninstallCommand(ISdkVersionManager manager, IAnsiConsole console) : AsyncCommand<UninstallCommand.Settings>
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
        if (string.IsNullOrWhiteSpace(version))
        {
            var installed = await manager.ListInstalledAsync(CommandHelpers.IsKnownCandidate(settings.CandidateOrVersion) ? candidateName : null, cancellationToken);
            if (installed.Count == 0)
            {
                CommandHelpers.WriteWarning(console, "No SDK versions are installed locally.");
                return 0;
            }

            var selected = console.Prompt(
                new SelectionPrompt<InstalledChoice>()
                    .Title("Select an SDK version to uninstall")
                    .PageSize(10)
                    .UseConverter(choice => $"{choice.CandidateName} {choice.Alias}")
                    .AddChoices(installed.OrderBy(item => item.CandidateName, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Alias, StringComparer.OrdinalIgnoreCase).Select(item => new InstalledChoice(item.CandidateName, item.Alias))));
            candidateName = selected.CandidateName;
            version = selected.Alias;
        }

        CommandHelpers.WriteHeader(console, $"uninstall {candidateName} {version}");
        await CommandHelpers.RunProgressAsync(console, $"Removing {candidateName} {version}", () => manager.UninstallAsync(candidateName, version, cancellationToken));
        CommandHelpers.WriteSuccess(console, $"Removed {candidateName} {version} and its local metadata/cache.");
        return 0;
    }

    private sealed record InstalledChoice(string CandidateName, string Alias);
}
