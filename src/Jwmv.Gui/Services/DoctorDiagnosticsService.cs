using System.Diagnostics;
using System.Globalization;
using Jwmv.Core;
using Jwmv.Core.Abstractions;
using Jwmv.Core.Models;
using Jwmv.Core.Utilities;
using Jwmv.Gui.Localization;
using Jwmv.Infrastructure.Windows;

namespace Jwmv.Gui.Services;

public sealed class DoctorDiagnosticsService(
    IJavaVersionManager javaVersionManager,
    IAppContext appContext,
    IShellProfileIntegrationService integrationService,
    LocalizationService localization)
{
    public async Task<DoctorReport> RunAsync(CancellationToken cancellationToken)
    {
        var current = await javaVersionManager.ResolveCurrentAsync(null, cancellationToken);
        var installed = await javaVersionManager.ListInstalledAsync(cancellationToken);
        var profilePath = integrationService.GetProfilePath(ShellKind.PowerShell);
        var profileIntegrated = File.Exists(profilePath)
            && (await File.ReadAllTextAsync(profilePath, cancellationToken)).Contains("jwmv initialize", StringComparison.Ordinal);
        var processPathEntries = PathTools.SplitPath(appContext.GetEnvironmentVariable("Path"));
        var userPathValue = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User);
        var userPathEntries = PathTools.SplitPath(userPathValue);
        var processJavaHome = appContext.GetEnvironmentVariable("JAVA_HOME");
        var userJavaHome = Environment.GetEnvironmentVariable("JAVA_HOME", EnvironmentVariableTarget.User);
        var defaultAlias = Environment.GetEnvironmentVariable(JwmvConstants.DefaultAliasVariable, EnvironmentVariableTarget.User);
        var defaultHome = Environment.GetEnvironmentVariable(JwmvConstants.DefaultHomeVariable, EnvironmentVariableTarget.User);
        var defaultBin = Environment.GetEnvironmentVariable(JwmvConstants.DefaultBinVariable, EnvironmentVariableTarget.User);
        var whereJava = await RunWhereAsync("java", cancellationToken);
        var shellIntegrated = string.Equals(appContext.GetEnvironmentVariable(JwmvConstants.ShellIntegrationVariable), "1", StringComparison.Ordinal);

        var facts = new List<DoctorFact>
        {
            new(localization["Doctor_Fact_ShellIntegration"], shellIntegrated ? localization["Doctor_Enabled"] : localization["Doctor_Disabled"]),
            new(localization["Doctor_Fact_Profile"], profilePath),
            new(localization["Doctor_Fact_ProfileBlock"], profileIntegrated ? localization["Common_Yes"] : localization["Common_No"]),
            new(localization["Doctor_Fact_Installed"], installed.Count.ToString(CultureInfo.InvariantCulture)),
            new(localization["Doctor_Fact_ResolvedAlias"], current.IsResolved ? current.Alias! : localization["Common_None"]),
            new(localization["Doctor_Fact_ResolvedSource"], current.Source.ToString()),
            new(localization["Doctor_Fact_ProcessJavaHome"], processJavaHome ?? localization["Common_NotSet"]),
            new(localization["Doctor_Fact_UserJavaHome"], userJavaHome ?? localization["Common_NotSet"]),
            new(localization["Doctor_Fact_UserDefaultAlias"], defaultAlias ?? localization["Common_NotSet"]),
            new(localization["Doctor_Fact_UserDefaultBin"], defaultBin ?? localization["Common_NotSet"]),
            new(localization["Doctor_Fact_ProcessPath"], FormatPathEntries(processPathEntries)),
            new(localization["Doctor_Fact_UserPath"], FormatPathEntries(userPathEntries)),
            new(localization["Doctor_Fact_WhereJava"], whereJava.Count == 0 ? localization["Common_None"] : string.Join(Environment.NewLine, whereJava))
        };

        var machineShadows = string.IsNullOrWhiteSpace(defaultBin)
            ? []
            : JavaPathPrecedence.GetMachinePathEntriesThatShadowJava(defaultBin);
        var issues = BuildIssues(current, processJavaHome, defaultHome, defaultBin, processPathEntries, whereJava, profileIntegrated, machineShadows);
        return new DoctorReport(facts, issues, profilePath, profileIntegrated);
    }

    private string FormatPathEntries(IReadOnlyList<string> entries) =>
        entries.Count == 0
            ? localization["Common_NotSet"]
            : string.Join(Environment.NewLine, entries.Take(5));

    private List<string> BuildIssues(
        ActiveJavaSelection current,
        string? processJavaHome,
        string? defaultHome,
        string? defaultBin,
        IReadOnlyList<string> processPathEntries,
        IReadOnlyList<string> whereJava,
        bool profileIntegrated,
        IReadOnlyList<string> machineShadows)
    {
        var issues = new List<string>();

        if (!profileIntegrated)
        {
            issues.Add(localization["Doctor_Issue_NoProfile"]);
        }

        if (current.IsResolved && !string.IsNullOrWhiteSpace(defaultHome) && !string.Equals(current.JavaHome, defaultHome, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(localization.Format("Doctor_Issue_HomeMismatch", current.JavaHome!, defaultHome));
        }

        if (machineShadows.Count > 0 && !string.IsNullOrWhiteSpace(defaultBin))
        {
            issues.Add(JavaPathPrecedence.BuildShadowWarning(machineShadows, defaultBin));
        }

        if (!string.IsNullOrWhiteSpace(defaultBin) && (processPathEntries.Count == 0 || !string.Equals(processPathEntries[0], defaultBin, StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(localization.Format("Doctor_Issue_PathOrder", defaultBin));
        }

        if (current.IsResolved && !string.IsNullOrWhiteSpace(processJavaHome) && !string.Equals(processJavaHome, current.JavaHome, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(localization.Format("Doctor_Issue_ProcessJavaHome", processJavaHome, current.JavaHome!));
        }

        if (current.IsResolved && whereJava.Count > 0)
        {
            var expectedJava = Path.Combine(current.BinDirectory!, "java.exe");
            if (!string.Equals(whereJava[0], expectedJava, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(localization.Format("Doctor_Issue_WhereOrder", whereJava[0], expectedJava));
            }
        }

        return issues;
    }

    private static async Task<IReadOnlyList<string>> RunWhereAsync(string command, CancellationToken cancellationToken)
    {
        var whereExecutable = ProcessLocator.FindSystemExecutable("where.exe");
        if (whereExecutable is null)
        {
            return [];
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = whereExecutable,
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return [];
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                return [];
            }

            return output
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }
}

public sealed record DoctorFact(string Name, string Value);

public sealed record DoctorReport(
    IReadOnlyList<DoctorFact> Facts,
    IReadOnlyList<string> Issues,
    string ProfilePath,
    bool ProfileIntegrated);
