using Jwmv.Core.Models;
using Spectre.Console;

namespace Jwmv.Cli.Commands;

internal static class CommandHelpers
{
    public const int WideTableWidth = 116;
    public const int MediumTableWidth = 88;

    private static readonly HashSet<string> KnownCandidates = new(StringComparer.OrdinalIgnoreCase)
    {
        "java",
        "gradle",
        "maven",
        "kotlin"
    };

    public static ShellKind ParseShell(string? shell) =>
        string.IsNullOrWhiteSpace(shell) || string.Equals(shell, "powershell", StringComparison.OrdinalIgnoreCase) || string.Equals(shell, "pwsh", StringComparison.OrdinalIgnoreCase)
            ? ShellKind.PowerShell
            : throw new ArgumentOutOfRangeException(nameof(shell), shell, "Only PowerShell is supported.");

    public static bool IsKnownCandidate(string? value) =>
        !string.IsNullOrWhiteSpace(value) && KnownCandidates.Contains(value);

    public static (string CandidateName, string? Version) ResolveCandidateAndVersion(string? candidateOrVersion, string? version)
    {
        if (string.IsNullOrWhiteSpace(candidateOrVersion))
        {
            return ("java", version);
        }

        return IsKnownCandidate(candidateOrVersion)
            ? (candidateOrVersion.Trim().ToLowerInvariant(), version)
            : ("java", candidateOrVersion);
    }

    public static Table CreateTable() => new Table()
        .AsciiBorder()
        .BorderColor(Color.Grey);

    public static Progress CreateProgress(IAnsiConsole console) => console.Progress()
        .AutoClear(false)
        .HideCompleted(false)
        .AutoRefresh(true)
        .Columns(
        [
            new TaskDescriptionColumn { Alignment = Justify.Left },
            new ProgressBarColumn
            {
                Width = IsWide(console) ? 42 : 24,
                CompletedStyle = new Style(Color.SpringGreen2),
                FinishedStyle = new Style(Color.Green),
                RemainingStyle = new Style(Color.Grey)
            },
            new PercentageColumn(),
            new RemainingTimeColumn(),
            new SpinnerColumn
            {
                Spinner = Spinner.Known.Dots,
                Style = new Style(Color.DeepSkyBlue1),
                CompletedText = "[green][[x]][/]",
                CompletedStyle = new Style(Color.Green)
            }
        ]);

    public static void WriteHeader(IAnsiConsole console, string title)
    {
        var width = Math.Clamp(ConsoleWidth(console), 44, 96);
        var line = new string('=', width);
        console.MarkupLine($"[grey]{line}[/]");
        console.MarkupLine($"[bold deepskyblue1]{Markup.Escape(title)}[/]");
        console.MarkupLine($"[grey]{line}[/]");
    }

    public static async Task<T?> RunInstallProgressAsync<T>(
        IAnsiConsole console,
        string title,
        Func<IProgress<InstallProgressUpdate>, Task<T>> action)
    {
        T? result = default;
        await CreateProgress(console).StartAsync(async progressContext =>
        {
            var task = progressContext.AddTask($"[deepskyblue1]{Markup.Escape(title)}[/]", maxValue: 100);
            var progress = new Progress<InstallProgressUpdate>(update => UpdateProgress(task, update.Status, update.Percentage));
            result = await action(progress);
            UpdateProgress(task, $"{title} complete", 100);
        });

        return result;
    }

    public static async Task<T?> RunSelfUpdateProgressAsync<T>(
        IAnsiConsole console,
        string title,
        Func<IProgress<SelfUpdateProgressUpdate>, Task<T>> action)
    {
        T? result = default;
        await CreateProgress(console).StartAsync(async progressContext =>
        {
            var task = progressContext.AddTask($"[deepskyblue1]{Markup.Escape(title)}[/]", maxValue: 100);
            var progress = new Progress<SelfUpdateProgressUpdate>(update => UpdateProgress(task, update.Status, update.Percentage));
            result = await action(progress);
            UpdateProgress(task, $"{title} complete", 100);
        });

        return result;
    }

    public static async Task RunProgressAsync(IAnsiConsole console, string title, Func<Task> action)
    {
        await CreateProgress(console).StartAsync(async progressContext =>
        {
            var task = progressContext.AddTask($"[deepskyblue1]{Markup.Escape(title)}[/]", maxValue: 100);
            task.Value(12);
            await action();
            UpdateProgress(task, $"{title} complete", 100);
        });
    }

    public static void WriteSuccess(IAnsiConsole console, string message) =>
        console.MarkupLine($"{CheckBox("green", "x")} [green]{Markup.Escape(message)}[/]");

    public static void WriteInfo(IAnsiConsole console, string message) =>
        console.MarkupLine($"{CheckBox("deepskyblue1", "i")} [grey]{Markup.Escape(message)}[/]");

    public static void WriteWarning(IAnsiConsole console, string message) =>
        console.MarkupLine($"{CheckBox("yellow", "!")} [yellow]{Markup.Escape(message)}[/]");

    public static void WriteFailure(IAnsiConsole console, string message) =>
        console.MarkupLine($"{CheckBox("red", "x")} [red]{Markup.Escape(message)}[/]");

    public static string CheckBox(string color, string value) => $"[{color}][[{Markup.Escape(value)}]][/]";

    public static int ConsoleWidth(IAnsiConsole console)
    {
        try
        {
            if (console.Profile.Width > 0)
            {
                return console.Profile.Width;
            }
        }
        catch (InvalidOperationException)
        {
        }

        try
        {
            return Console.WindowWidth > 0 ? Console.WindowWidth : 100;
        }
        catch (IOException)
        {
            return 100;
        }
        catch (PlatformNotSupportedException)
        {
            return 100;
        }
    }

    public static bool IsWide(IAnsiConsole console) => ConsoleWidth(console) >= WideTableWidth;

    public static bool IsMediumOrWider(IAnsiConsole console) => ConsoleWidth(console) >= MediumTableWidth;

    public static bool CanPrompt(IAnsiConsole console) =>
        console.Profile.Capabilities.Interactive &&
        !Console.IsInputRedirected &&
        !Console.IsOutputRedirected;

    public static string Header(string value) => $"[bold deepskyblue1]{Markup.Escape(value)}[/]";

    public static string Candidate(string value) => $"[{CandidateColor(value)} bold]{Markup.Escape(value)}[/]";

    public static string Version(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "[grey]-[/]" : $"[bold springgreen2]{Markup.Escape(value)}[/]";

    public static string Alias(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "[grey]-[/]" : $"[lightskyblue1]{Markup.Escape(value)}[/]";

    public static string Url(Uri? uri)
    {
        if (uri is null)
        {
            return "[grey]-[/]";
        }

        var value = uri.ToString();
        return $"[link={Markup.Escape(value)}][underline blue]{Markup.Escape(value)}[/][/]";
    }

    public static string Status(IEnumerable<string> parts)
    {
        var rendered = parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToArray();
        return rendered.Length == 0 ? "[grey]-[/]" : string.Join("[grey], [/]", rendered);
    }

    public static string Support(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "-", StringComparison.Ordinal))
        {
            return "[grey]-[/]";
        }

        var color = value.Equals("lts", StringComparison.OrdinalIgnoreCase)
            ? "green"
            : value.Equals("sts", StringComparison.OrdinalIgnoreCase)
                ? "yellow"
                : "grey";
        return $"[{color}]{Markup.Escape(value)}[/]";
    }

    public static string Muted(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "[grey]-[/]" : $"[grey]{Markup.Escape(value)}[/]";

    public static string Text(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "[grey]-[/]" : Markup.Escape(value);

    public static string Shorten(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        if (maxLength < 4 || value.Length <= maxLength)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, maxLength - 3), "...");
    }

    private static string CandidateColor(string value) =>
        value.ToLowerInvariant() switch
        {
            "java" => "orange1",
            "gradle" => "dodgerblue1",
            "maven" => "red1",
            "kotlin" => "mediumorchid1",
            _ => "deepskyblue1"
        };

    private static void UpdateProgress(ProgressTask task, string status, double percentage)
    {
        task.Description($"[deepskyblue1]{Markup.Escape(status)}[/]");
        task.Value(Math.Clamp(percentage, 0d, 100d));
    }
}
