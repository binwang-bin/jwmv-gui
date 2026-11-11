using Jwmv.Core.Abstractions;
using Jwmv.Core.Models;
using Jwmv.Infrastructure.Windows;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Jwmv.Cli.Commands;

public sealed class DefaultCommand(ISdkVersionManager manager, IAnsiConsole console) : AsyncCommand<DefaultCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<candidate-or-version>")]
        public string CandidateOrVersion { get; init; } = string.Empty;

        [CommandArgument(1, "[version]")]
        public string? Version { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var (candidateName, version) = CommandHelpers.ResolveCandidateAndVersion(settings.CandidateOrVersion, settings.Version);
        if (string.IsNullOrWhiteSpace(version))
        {
            CommandHelpers.WriteFailure(console, "A version is required.");
            return -1;
        }

        CommandHelpers.WriteHeader(console, $"default {candidateName}");
        InstalledSdkVersion? installed = null;
        await CommandHelpers.RunProgressAsync(console, $"Setting default {candidateName} {version}", async () =>
        {
            installed = await manager.SetDefaultAsync(candidateName, version, cancellationToken);
        });

        CommandHelpers.WriteSuccess(console, $"Default {candidateName} set to {installed!.Alias}.");
        CommandHelpers.WriteInfo(console, $"Home: {installed.HomeDirectory}");
        CommandHelpers.WriteInfo(console, "Open a new Windows Terminal tab/window so it reloads environment variables.");

        if (string.Equals(candidateName, "java", StringComparison.OrdinalIgnoreCase))
        {
            var shadows = JavaPathPrecedence.GetMachinePathEntriesThatShadowJava(installed.BinDirectory);
            if (shadows.Count > 0)
            {
                CommandHelpers.WriteWarning(console, JavaPathPrecedence.BuildShadowWarning(shadows, installed.BinDirectory));
            }
        }

        return 0;
    }
}
