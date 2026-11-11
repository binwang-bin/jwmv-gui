using Jwmv.Core.Abstractions;
using Jwmv.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Jwmv.Cli.Commands;

public sealed class ListCommand(ISdkVersionManager manager, IAnsiConsole console) : AsyncCommand<ListCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[candidate-or-filter]")]
        public string? CandidateOrFilter { get; init; }

        [CommandArgument(1, "[filter]")]
        public string? Filter { get; init; }

        [CommandOption("-r|--refresh")]
        public bool Refresh { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var candidateName = CommandHelpers.IsKnownCandidate(settings.CandidateOrFilter) ? settings.CandidateOrFilter : "java";
        var filter = CommandHelpers.IsKnownCandidate(settings.CandidateOrFilter) ? settings.Filter : settings.CandidateOrFilter;
        var sdkAvailable = await manager.ListAvailableAsync(new SdkCatalogQuery
        {
            CandidateName = candidateName,
            VersionFilter = filter,
            ForceRefresh = settings.Refresh
        }, cancellationToken);
        var installed = await manager.ListInstalledAsync(candidateName, cancellationToken);
        var current = await manager.ResolveCurrentAsync(candidateName!, null, cancellationToken);
        var installedAliases = installed.ToDictionary(item => item.Alias, StringComparer.OrdinalIgnoreCase);
        var wide = CommandHelpers.IsWide(console);
        var medium = CommandHelpers.IsMediumOrWider(console);

        var table = CommandHelpers.CreateTable();
        if (medium)
        {
            table.AddColumn(CommandHelpers.Header("Candidate"));
        }

        table.AddColumn(CommandHelpers.Header("Version"));
        table.AddColumn(CommandHelpers.Header("Alias"));
        if (wide)
        {
            table.AddColumn(CommandHelpers.Header("Support"));
        }

        table.AddColumn(CommandHelpers.Header("Status"));

        foreach (var package in sdkAvailable)
        {
            var statusParts = new List<string>();
            if (installedAliases.ContainsKey(package.Alias))
            {
                statusParts.Add("[green]installed[/]");
            }

            if (string.Equals(current.Alias, package.Alias, StringComparison.OrdinalIgnoreCase))
            {
                statusParts.Add($"[yellow]{Markup.Escape(current.Source.ToString())}[/]");
            }

            var row = new List<string>();
            if (medium)
            {
                row.Add(CommandHelpers.Candidate(package.CandidateName));
            }

            row.Add(CommandHelpers.Version(package.Version));
            row.Add(CommandHelpers.Alias(package.Alias));
            if (wide)
            {
                row.Add(CommandHelpers.Support(package.SupportTerm));
            }

            row.Add(CommandHelpers.Status(statusParts));
            table.AddRow(row.ToArray());
        }

        console.Write(table);
        CommandHelpers.WriteSuccess(console, $"{sdkAvailable.Count} package(s) shown, {installed.Count} installed locally.");
        return 0;
    }
}
