using Jwmv.Core.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Jwmv.Cli.Commands;

public sealed class UseCommand(ISdkVersionManager manager, IAppContext appContext, IAnsiConsole console) : AsyncCommand<UseCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<candidate-or-version>")]
        public string CandidateOrVersion { get; init; } = string.Empty;

        [CommandArgument(1, "[version]")]
        public string? Version { get; init; }

        [CommandOption("--shell <SHELL>")]
        public string? Shell { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var (candidateName, version) = CommandHelpers.ResolveCandidateAndVersion(settings.CandidateOrVersion, settings.Version);
        if (string.IsNullOrWhiteSpace(version))
        {
            CommandHelpers.WriteFailure(console, "A version is required.");
            return -1;
        }

        if (string.IsNullOrWhiteSpace(settings.Shell))
        {
            var exe = appContext.ExecutablePath;
            CommandHelpers.WriteHeader(console, $"use {candidateName} {version}");
            CommandHelpers.WriteWarning(console, "`use` cannot mutate the current shell by itself when you run the executable directly.");
            console.WriteLine("Use one of these PowerShell commands:");
            console.WriteLine($"Invoke-Expression ((& '{exe}' use '{candidateName}' '{version}' --shell powershell) -join [Environment]::NewLine)");
            console.WriteLine($"Or install shell integration once: & '{exe}' integrate");
            return 0;
        }

        var shell = CommandHelpers.ParseShell(settings.Shell);
        var script = await manager.BuildUseShellScriptAsync(candidateName, version, shell, cancellationToken);
        Console.WriteLine(script);
        return 0;
    }
}
