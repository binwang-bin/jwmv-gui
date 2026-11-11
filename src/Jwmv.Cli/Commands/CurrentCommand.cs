using Jwmv.Core;
using Jwmv.Core.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Jwmv.Cli.Commands;

public sealed class CurrentCommand(ISdkVersionManager manager, IAppContext appContext, IAnsiConsole console) : AsyncCommand<CurrentCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[candidate]")]
        public string? Candidate { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var selections = string.IsNullOrWhiteSpace(settings.Candidate)
            ? await manager.ResolveAllCurrentAsync(null, cancellationToken)
            : [await manager.ResolveCurrentAsync(settings.Candidate, null, cancellationToken)];
        if (selections.All(selection => !selection.IsResolved))
        {
            var current = selections.Count > 0 ? selections[0] : null;
            CommandHelpers.WriteWarning(console, "No active SDK version is currently resolved.");
            if (!string.Equals(appContext.GetEnvironmentVariable(JwmvConstants.ShellIntegrationVariable), "1", StringComparison.Ordinal))
            {
                console.MarkupLine("[grey]Hint:[/] `jwmv use` only affects the current PowerShell session when its output is evaluated.");
                console.WriteLine($"Run once: & '{appContext.ExecutablePath}' integrate");
                console.WriteLine("Then reload PowerShell or run `. $PROFILE`.");
            }
            else
            {
                console.MarkupLine("[grey]Shell integration is loaded.[/] Activate one with `jwmv use <version>` or persist one with `jwmv default <version>`.");
            }

            if (!string.IsNullOrWhiteSpace(current?.ProjectFilePath))
            {
                console.MarkupLine($"Project file found at [grey]{Markup.Escape(current.ProjectFilePath)}[/], but its Java version is not installed.");
            }

            return 0;
        }

        var medium = CommandHelpers.IsMediumOrWider(console);
        var table = CommandHelpers.CreateTable();
        table.AddColumn(CommandHelpers.Header("Candidate"));
        table.AddColumn(CommandHelpers.Header("Version"));
        table.AddColumn(CommandHelpers.Header("Source"));
        if (medium)
        {
            table.AddColumn(CommandHelpers.Header("Home"));
        }

        foreach (var current in selections.Where(selection => selection.IsResolved))
        {
            var row = new List<string>
            {
                CommandHelpers.Candidate(current.CandidateName),
                CommandHelpers.Alias(current.Alias ?? current.Version),
                $"[yellow]{Markup.Escape(current.Source.ToString())}[/]"
            };

            if (medium)
            {
                row.Add(CommandHelpers.Muted(current.HomeDirectory));
            }

            table.AddRow(row.ToArray());
        }

        console.Write(table);
        CommandHelpers.WriteSuccess(console, $"{selections.Count(selection => selection.IsResolved)} active SDK selection(s) resolved.");
        return 0;
    }
}
