using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jwmv.Core.Abstractions;
using Jwmv.Core.Models;
using Jwmv.Gui.Localization;
using Jwmv.Gui.Services;

namespace Jwmv.Gui.ViewModels;

public sealed partial class DoctorViewModel : ViewModelBase, IDisposable
{
    private readonly DoctorDiagnosticsService _diagnostics;
    private readonly IShellProfileIntegrationService _integrationService;
    private CancellationTokenSource? _runCts;
    private bool _disposed;

    [ObservableProperty]
    private string? _profilePath;

    [ObservableProperty]
    private bool _profileIntegrated;

    [ObservableProperty]
    private string _profileIntegratedText = string.Empty;

    [ObservableProperty]
    private string _findingsText = string.Empty;

    [ObservableProperty]
    private bool _hasFindings;

    [ObservableProperty]
    private bool _isFactsEmpty = true;

    public DoctorViewModel(
        DoctorDiagnosticsService diagnostics,
        IShellProfileIntegrationService integrationService,
        GuiBusyService busyService,
        LocalizationService localization)
        : base(busyService, localization)
    {
        _diagnostics = diagnostics;
        _integrationService = integrationService;
        ProfileIntegratedText = Loc["Common_No"];
        FindingsText = Loc["Doctor_EmptyFindingsHint"];
        localization.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<DoctorFact> Facts { get; } = [];

    public ObservableCollection<string> Issues { get; } = [];

    [RelayCommand(CanExecute = nameof(CanRunDoctor))]
    private async Task RunDoctorAsync()
    {
        _runCts?.Cancel();
        _runCts?.Dispose();
        _runCts = new CancellationTokenSource();
        var token = _runCts.Token;

        await RunBusyAsync(Loc["Doctor_Running"], async cancellationToken =>
        {
            var report = await _diagnostics.RunAsync(cancellationToken);
            Facts.Clear();
            foreach (var fact in report.Facts)
            {
                Facts.Add(fact);
            }

            Issues.Clear();
            foreach (var issue in report.Issues)
            {
                Issues.Add(issue);
            }

            HasFindings = true;
            IsFactsEmpty = Facts.Count == 0;
            FindingsText = Issues.Count == 0
                ? Loc["Doctor_NoIssues"]
                : string.Join(Environment.NewLine + Environment.NewLine, Issues);

            ProfilePath = report.ProfilePath;
            ProfileIntegrated = report.ProfileIntegrated;
            ProfileIntegratedText = report.ProfileIntegrated ? Loc["Common_Yes"] : Loc["Common_No"];
            InfoMessage = report.Issues.Count == 0
                ? Loc["Doctor_NoIssues"]
                : Loc.Format("Doctor_FoundIssues", report.Issues.Count);
        }, token);

        NotifyActionCommands();
    }

    [RelayCommand(CanExecute = nameof(CanCopyFindings))]
    private void CopyFindings()
    {
        if (string.IsNullOrWhiteSpace(FindingsText) || !HasFindings)
        {
            ErrorMessage = Loc["Doctor_NothingToCopy"];
            return;
        }

        if (!ClipboardHelper.TrySetText(FindingsText, out var findingsError))
        {
            var path = ClipboardHelper.SaveTextFile(
                Path.Combine(Path.GetTempPath(), "jwmv", "scripts"),
                "doctor-findings.txt",
                FindingsText);
            InfoMessage = Loc.Format("Doctor_SavedNotCopied", path);
            ErrorMessage = Loc.Format("Doctor_ClipboardFailed", findingsError ?? string.Empty);
            return;
        }

        InfoMessage = Loc["Doctor_FindingsCopied"];
    }

    [RelayCommand(CanExecute = nameof(CanCopyReport))]
    private void CopyReport()
    {
        if (Facts.Count == 0 && !HasFindings)
        {
            ErrorMessage = Loc["Doctor_NothingToCopy"];
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine(Loc["Doctor_Status"]);
        foreach (var fact in Facts)
        {
            builder.Append(fact.Name).Append(": ").AppendLine(fact.Value);
        }

        builder.AppendLine();
        builder.AppendLine(Loc["Doctor_Findings"]);
        builder.AppendLine(string.IsNullOrWhiteSpace(FindingsText) ? Loc["Doctor_NoIssues"] : FindingsText);

        var report = builder.ToString();
        if (!ClipboardHelper.TrySetText(report, out var reportError))
        {
            var path = ClipboardHelper.SaveTextFile(
                Path.Combine(Path.GetTempPath(), "jwmv", "scripts"),
                "doctor-report.txt",
                report);
            InfoMessage = Loc.Format("Doctor_SavedNotCopied", path);
            ErrorMessage = Loc.Format("Doctor_ClipboardFailed", reportError ?? string.Empty);
            return;
        }

        InfoMessage = Loc["Doctor_ReportCopied"];
    }

    [RelayCommand(CanExecute = nameof(CanIntegrate))]
    private async Task IntegrateAsync()
    {
        await RunBusyAsync(Loc["Doctor_WritingIntegrate"], async cancellationToken =>
        {
            var path = await _integrationService.EnsureIntegratedAsync(ShellKind.PowerShell, profilePath: null, cancellationToken);
            InfoMessage = Loc.Format("Doctor_IntegrateDone", path);
            await RunDoctorAsync();
        }, CancellationToken.None);
    }

    private bool CanRunDoctor() => IsIdle;

    private bool CanIntegrate() => IsIdle;

    private bool CanCopyFindings() => HasFindings && !string.IsNullOrWhiteSpace(FindingsText);

    private bool CanCopyReport() => Facts.Count > 0 || HasFindings;

    protected override void OnBusyStateChanged() => NotifyActionCommands();

    private void NotifyActionCommands()
    {
        RunDoctorCommand.NotifyCanExecuteChanged();
        IntegrateCommand.NotifyCanExecuteChanged();
        CopyFindingsCommand.NotifyCanExecuteChanged();
        CopyReportCommand.NotifyCanExecuteChanged();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        ProfileIntegratedText = ProfileIntegrated ? Loc["Common_Yes"] : Loc["Common_No"];
        if (!HasFindings)
        {
            FindingsText = Loc["Doctor_EmptyFindingsHint"];
        }
    }

    public override Task InitializeAsync(CancellationToken cancellationToken) => RunDoctorAsync();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Loc.LanguageChanged -= OnLanguageChanged;
        _runCts?.Cancel();
        _runCts?.Dispose();
        _disposed = true;
    }
}
