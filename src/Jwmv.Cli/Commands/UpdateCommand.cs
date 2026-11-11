using Jwmv.Core.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Jwmv.Cli.Commands;

public sealed class UpdateCommand(ISdkVersionManager manager, IAnsiConsole console) : AsyncCommand<UpdateCommand.Settings>
{
    public sealed class Settings : CommandSettings;

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        CommandHelpers.WriteHeader(console, "update catalog");
        await CommandHelpers.RunProgressAsync(console, "Refreshing SDK catalog", () => manager.RefreshCatalogAsync(cancellationToken));
        CommandHelpers.WriteSuccess(console, "Catalog cache refreshed from SDK providers.");
        return 0;
    }
}
