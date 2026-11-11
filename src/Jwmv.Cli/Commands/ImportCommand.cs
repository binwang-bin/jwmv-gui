using Jwmv.Core.Abstractions;
using Jwmv.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Jwmv.Cli.Commands;

public sealed class ImportCommand(ISdkVersionManager manager, IAnsiConsole console) : AsyncCommand<ImportCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<path>")]
        public string Path { get; init; } = string.Empty;

        [CommandOption("--candidate <NAME>")]
        [System.ComponentModel.Description("SDK candidate to import as (default: java).")]
        public string Candidate { get; init; } = "java";

        [CommandOption("--alias <ALIAS>")]
        public string? Alias { get; init; }

        [CommandOption("-d|--default")]
        public bool SetDefault { get; init; }

        [CommandOption("--allow-jre")]
        [System.ComponentModel.Description("Allow importing a Java runtime that has java.exe but no javac.exe.")]
        public bool AllowJre { get; init; }

        [CommandOption("--all")]
        [System.ComponentModel.Description("Import every SDK home found under the given parent directory.")]
        public bool ImportAll { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        CommandHelpers.WriteHeader(console, $"import {settings.Candidate}");
        var results = await manager.ImportLocalAsync(new ImportLocalSdkRequest
        {
            CandidateName = settings.Candidate,
            Path = settings.Path,
            Alias = settings.Alias,
            SetAsDefault = settings.SetDefault,
            AllowJre = settings.AllowJre,
            ImportAll = settings.ImportAll
        }, cancellationToken);

        foreach (var result in results)
        {
            if (result.AlreadyInstalled)
            {
                CommandHelpers.WriteInfo(console, $"{result.InstalledVersion.CandidateName} {result.InstalledVersion.Alias} is already imported ({result.InstalledVersion.HomeDirectory}).");
            }
            else
            {
                CommandHelpers.WriteSuccess(console, $"Imported {result.InstalledVersion.CandidateName} {result.InstalledVersion.Alias} from {result.InstalledVersion.HomeDirectory}");
            }

            if (result.DefaultWasUpdated)
            {
                CommandHelpers.WriteSuccess(console, $"Default {result.InstalledVersion.CandidateName} set to {result.InstalledVersion.Alias}");
            }

            if (!string.IsNullOrWhiteSpace(result.IntegrityWarning))
            {
                CommandHelpers.WriteWarning(console, result.IntegrityWarning);
            }
        }

        return 0;
    }
}
