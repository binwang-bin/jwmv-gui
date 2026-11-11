using Jwmv.Core.Abstractions;
using Spectre.Console.Cli;

namespace Jwmv.Cli.Commands;

public sealed class HomeCommand(ISdkVersionManager manager) : AsyncCommand<HomeCommand.Settings>
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
        var home = await manager.GetHomeAsync(candidateName, version, cancellationToken);
        if (!string.IsNullOrWhiteSpace(home))
        {
            Console.WriteLine(home);
        }

        return 0;
    }
}
