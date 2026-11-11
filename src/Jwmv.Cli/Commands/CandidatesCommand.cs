using Jwmv.Core.Abstractions;
using Jwmv.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Jwmv.Cli.Commands;

public sealed class CandidatesCommand(ISdkVersionManager manager, IAnsiConsole console) : AsyncCommand<CandidatesCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[filter]")]
        public string? Filter { get; init; }

        [CommandOption("-i|--interactive")]
        public bool Interactive { get; init; }

        [CommandOption("--no-interactive")]
        public bool NoInteractive { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var candidates = await manager.ListCandidatesAsync(settings.Filter, cancellationToken);
        var wide = CommandHelpers.IsWide(console);
        var medium = CommandHelpers.IsMediumOrWider(console);

        console.MarkupLine("[bold deepskyblue1]SDK candidates[/] [grey]for jwmv[/]");
        console.MarkupLine("[grey]Use[/] [blue]jwmv list <candidate>[/] [grey]or pick one below when your terminal supports prompts.[/]");

        var table = CommandHelpers.CreateTable();
        table.AddColumn(CommandHelpers.Header("Candidate"));
        table.AddColumn(CommandHelpers.Header("Latest"));
        table.AddColumn(CommandHelpers.Header("Description"));
        if (medium)
        {
            table.AddColumn(CommandHelpers.Header("Website"));
        }

        if (wide)
        {
            table.AddColumn(CommandHelpers.Header("Try"));
        }

        foreach (var candidate in candidates)
        {
            var row = new List<string>
            {
                CommandHelpers.Candidate(candidate.Name),
                CommandHelpers.Version(candidate.LatestVersion),
                CommandHelpers.Text(CommandHelpers.Shorten(candidate.Description, wide ? 44 : 28))
            };

            if (medium)
            {
                row.Add(CommandHelpers.Url(candidate.WebsiteUri));
            }

            if (wide)
            {
                row.Add($"[grey]jwmv install[/] [blue]{Markup.Escape(candidate.Name)}[/]");
            }

            table.AddRow(row.ToArray());
        }

        console.Write(table);
        CommandHelpers.WriteSuccess(console, $"{candidates.Count} candidate(s) shown.");

        if (ShouldPrompt(settings) && candidates.Count > 0)
        {
            await PromptForCandidateDetailsAsync(candidates.Select(candidate => candidate.Name), cancellationToken);
        }
        else if (settings.Interactive && !CommandHelpers.CanPrompt(console))
        {
            CommandHelpers.WriteWarning(console, "Interactive prompt is not available in this terminal.");
        }

        return 0;
    }

    private bool ShouldPrompt(Settings settings) =>
        !settings.NoInteractive &&
        CommandHelpers.CanPrompt(console) &&
        (settings.Interactive || string.IsNullOrWhiteSpace(settings.Filter));

    private async Task PromptForCandidateDetailsAsync(IEnumerable<string> candidateNames, CancellationToken cancellationToken)
    {
        var exitLabel = "Exit";
        var choices = candidateNames.Order(StringComparer.OrdinalIgnoreCase).Append(exitLabel).ToArray();
        var selected = console.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Inspect a candidate[/]")
                .PageSize(Math.Min(8, choices.Length))
                .HighlightStyle(new Style(Color.Black, Color.SpringGreen2))
                .AddChoices(choices));

        if (selected.Equals(exitLabel, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var packages = await manager.ListAvailableAsync(new SdkCatalogQuery
        {
            CandidateName = selected
        }, cancellationToken);

        console.WriteLine();
        console.MarkupLine($"[bold]{CommandHelpers.Candidate(selected)}[/] [grey]latest stable packages[/]");

        var table = CommandHelpers.CreateTable();
        table.AddColumn(CommandHelpers.Header("Version"));
        table.AddColumn(CommandHelpers.Header("Alias"));
        table.AddColumn(CommandHelpers.Header("Support"));

        foreach (var package in packages.Take(8))
        {
            table.AddRow(
                CommandHelpers.Version(package.Version),
                CommandHelpers.Alias(package.Alias),
                CommandHelpers.Support(package.SupportTerm));
        }

        console.Write(table);
        var suggestedVersion = packages.Count > 0 ? packages[0].Alias : "<version>";
        console.MarkupLine($"[grey]Next:[/] [blue]jwmv list {Markup.Escape(selected)}[/] [grey]or[/] [blue]jwmv install {Markup.Escape(selected)} {Markup.Escape(suggestedVersion)}[/]");
    }
}
