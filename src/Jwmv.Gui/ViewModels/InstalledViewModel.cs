using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jwmv.Core.Abstractions;
using Jwmv.Core.Models;
using Jwmv.Gui.Localization;
using Jwmv.Gui.Services;
using Microsoft.Win32;

namespace Jwmv.Gui.ViewModels;

public sealed partial class InstalledViewModel : ViewModelBase, IDisposable
{
    private static readonly string[] KnownCandidates = ["java", "gradle", "maven", "kotlin"];

    private readonly ISdkVersionManager _manager;
    private CancellationTokenSource? _refreshCts;
    private int _refreshGeneration;
    private bool _disposed;
    private bool _suppressFilterRefresh;

    [ObservableProperty]
    private string? _candidateFilter;

    [ObservableProperty]
    private string _importCandidate = "java";

    [ObservableProperty]
    private InstalledSdkRow? _selectedInstalled;

    [ObservableProperty]
    private string? _useScript;

    [ObservableProperty]
    private bool _importAll = true;

    [ObservableProperty]
    private bool _isImportExpanded = true;

    [ObservableProperty]
    private bool _isUseScriptExpanded;

    [ObservableProperty]
    private bool _isListEmpty = true;

    public InstalledViewModel(ISdkVersionManager manager, GuiBusyService busyService, LocalizationService localization)
        : base(busyService, localization)
    {
        _manager = manager;
        RebuildCandidateFilters();
        localization.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<InstalledSdkRow> InstalledVersions { get; } = [];

    public ObservableCollection<string> CandidateFilterNames { get; } = [];

    public ObservableCollection<string> ImportCandidateNames { get; } = [];

    public bool HasSelection => SelectedInstalled is not null;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        var generation = Interlocked.Increment(ref _refreshGeneration);
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = new CancellationTokenSource();
        var token = _refreshCts.Token;

        await RunBusyAsync(Loc["Installed_Loading"], async cancellationToken =>
        {
            var filter = IsAllFilter(CandidateFilter) ? null : CandidateFilter;
            var installed = await _manager.ListInstalledAsync(filter, cancellationToken);
            var config = await _manager.GetConfigAsync(cancellationToken);
            var defaultBadge = Loc["Installed_DefaultBadge"];

            // Resolve current selection per candidate so an existing JAVA_HOME / *_HOME
            // that already points at an imported home can show the default badge.
            var environmentHomes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidateName in installed.Select(item => item.CandidateName).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (HasConfiguredDefault(config, candidateName))
                {
                    continue;
                }

                var current = await _manager.ResolveCurrentAsync(candidateName, null, cancellationToken);
                if (current.Source == JavaActivationSource.Environment &&
                    !string.IsNullOrWhiteSpace(current.HomeDirectory))
                {
                    environmentHomes[candidateName] = current.HomeDirectory;
                }
            }

            if (generation != Volatile.Read(ref _refreshGeneration))
            {
                return;
            }

            InstalledVersions.Clear();
            foreach (var item in installed
                         .Select(version =>
                         {
                             var isDefault = IsConfiguredDefault(config, version)
                                 || IsEnvironmentDefault(environmentHomes, version);
                             return new InstalledSdkRow
                             {
                                 Version = version,
                                 IsDefault = isDefault,
                                 DefaultLabel = isDefault ? defaultBadge : string.Empty
                             };
                         })
                         .OrderBy(item => item.CandidateName, StringComparer.OrdinalIgnoreCase)
                         .ThenByDescending(item => item.IsDefault)
                         .ThenByDescending(item => item.Alias, StringComparer.OrdinalIgnoreCase))
            {
                InstalledVersions.Add(item);
            }

            IsListEmpty = InstalledVersions.Count == 0;
            InfoMessage = Loc.Format("Installed_Found", InstalledVersions.Count);
        }, token);

        NotifyActionCommands();
    }

    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task ImportLocalAsync()
    {
        var candidate = string.IsNullOrWhiteSpace(ImportCandidate) ? "java" : ImportCandidate.Trim();
        var dialog = new OpenFolderDialog
        {
            Title = Loc.Format("Installed_ImportSelectDirectoryFor", candidate),
            Multiselect = false
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            return;
        }

        var results = await RunBusyAsync(Loc.Format("Installed_Importing", dialog.FolderName), async cancellationToken =>
        {
            return await _manager.ImportLocalAsync(new ImportLocalSdkRequest
            {
                CandidateName = candidate,
                Path = dialog.FolderName,
                AllowJre = false,
                ImportAll = ImportAll
            }, cancellationToken);
        }, CancellationToken.None);

        if (results is null || results.Count == 0)
        {
            return;
        }

        var lines = new List<string>
        {
            Loc.Format("Installed_ImportBatch", results.Count)
        };
        foreach (var result in results)
        {
            lines.Add(result.AlreadyInstalled
                ? Loc.Format("Installed_ImportAlready", result.InstalledVersion.Alias, result.InstalledVersion.HomeDirectory)
                : Loc.Format("Installed_Imported", result.InstalledVersion.Alias, result.InstalledVersion.HomeDirectory));
            if (result.DefaultWasUpdated)
            {
                lines.Add(Loc.Format("Installed_DefaultSet", result.InstalledVersion.CandidateName, result.InstalledVersion.Alias));
            }

            if (!string.IsNullOrWhiteSpace(result.IntegrityWarning))
            {
                lines.Add(result.IntegrityWarning);
            }
        }

        InfoMessage = string.Join(Environment.NewLine, lines);

        if (!string.Equals(CandidateFilter, candidate, StringComparison.OrdinalIgnoreCase))
        {
            _suppressFilterRefresh = true;
            CandidateFilter = candidate;
            _suppressFilterRefresh = false;
        }

        await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanMutateSelection))]
    private async Task SetDefaultAsync()
    {
        if (SelectedInstalled is null)
        {
            ErrorMessage = Loc["Installed_SelectFirst"];
            return;
        }

        var selected = SelectedInstalled.Version;
        await RunBusyAsync(Loc.Format("Installed_SettingDefault", selected.CandidateName, selected.Alias), async cancellationToken =>
        {
            await _manager.SetDefaultAsync(selected.CandidateName, selected.Alias, cancellationToken);
            InfoMessage = Loc.Format("Installed_DefaultSet", selected.CandidateName, selected.Alias)
                + Environment.NewLine
                + Loc["Installed_DefaultReloadTerminal"];

            if (string.Equals(selected.CandidateName, "java", StringComparison.OrdinalIgnoreCase))
            {
                var shadows = Jwmv.Infrastructure.Windows.JavaPathPrecedence.GetMachinePathEntriesThatShadowJava(selected.BinDirectory);
                if (shadows.Count > 0)
                {
                    InfoMessage += Environment.NewLine
                        + Jwmv.Infrastructure.Windows.JavaPathPrecedence.BuildShadowWarning(shadows, selected.BinDirectory);
                }
            }
        }, CancellationToken.None);

        var status = InfoMessage;
        await RefreshAsync();
        if (!string.IsNullOrWhiteSpace(status))
        {
            InfoMessage = status;
        }
    }

    [RelayCommand(CanExecute = nameof(CanMutateSelection))]
    private async Task UninstallAsync()
    {
        if (SelectedInstalled is null)
        {
            ErrorMessage = Loc["Installed_SelectFirst"];
            return;
        }

        var selected = SelectedInstalled.Version;
        var confirm = MessageBox.Show(
            Loc.Format("Installed_ConfirmUninstall", selected.CandidateName, selected.Alias),
            Loc["Installed_ConfirmUninstallTitle"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        await RunBusyAsync(Loc.Format("Installed_Uninstalling", selected.CandidateName, selected.Alias), async cancellationToken =>
        {
            await _manager.UninstallAsync(selected.CandidateName, selected.Alias, cancellationToken);
            InfoMessage = Loc.Format("Installed_Uninstalled", selected.CandidateName, selected.Alias);
        }, CancellationToken.None);

        await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanMutateSelection))]
    private async Task CopyHomeAsync()
    {
        if (SelectedInstalled is null)
        {
            ErrorMessage = Loc["Installed_SelectFirst"];
            return;
        }

        var home = await _manager.GetHomeAsync(
            SelectedInstalled.Version.CandidateName,
            SelectedInstalled.Version.Alias,
            CancellationToken.None);
        if (string.IsNullOrWhiteSpace(home))
        {
            ErrorMessage = Loc["Installed_HomeUnresolved"];
            return;
        }

        if (!ClipboardHelper.TrySetText(home, out var homeCopyError))
        {
            ErrorMessage = Loc.Format("Installed_ClipboardFailed", homeCopyError ?? string.Empty);
            return;
        }

        InfoMessage = Loc.Format("Installed_HomeCopied", home);
    }

    [RelayCommand(CanExecute = nameof(CanMutateSelection))]
    private async Task ExportUseScriptAsync()
    {
        if (SelectedInstalled is null)
        {
            ErrorMessage = Loc["Installed_SelectFirst"];
            return;
        }

        var selected = SelectedInstalled.Version;
        var script = await RunBusyAsync(
            Loc.Format("Installed_BuildingUse", selected.CandidateName, selected.Alias),
            cancellationToken => _manager.BuildUseShellScriptAsync(selected.CandidateName, selected.Alias, ShellKind.PowerShell, cancellationToken),
            CancellationToken.None);

        if (script is null)
        {
            return;
        }

        UseScript = script;
        IsUseScriptExpanded = true;
        var scriptPath = SaveUseScriptFile(selected, script);
        if (ClipboardHelper.TrySetText(script, out _))
        {
            InfoMessage = Loc.Format("Installed_UseCopiedWithFile", scriptPath);
            return;
        }

        InfoMessage = Loc.Format("Installed_UseSavedNotCopied", scriptPath);
    }

    private static string SaveUseScriptFile(InstalledSdkVersion selected, string script)
    {
        var safeCandidate = SanitizeFileToken(selected.CandidateName);
        var safeAlias = SanitizeFileToken(selected.Alias);
        var directory = Path.Combine(Path.GetTempPath(), "jwmv", "scripts");
        return ClipboardHelper.SaveTextFile(directory, $"use-{safeCandidate}-{safeAlias}.ps1", script);
    }

    private static string SanitizeFileToken(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(chars).Trim('.', ' ');
        return string.IsNullOrWhiteSpace(sanitized) ? "script" : sanitized;
    }

    partial void OnCandidateFilterChanged(string? value)
    {
        if (!IsAllFilter(value))
        {
            ImportCandidate = value!;
        }

        if (!_suppressFilterRefresh && !_disposed)
        {
            _ = RefreshAsync();
        }
    }

    partial void OnSelectedInstalledChanged(InstalledSdkRow? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        NotifyActionCommands();
    }

    private void RebuildCandidateFilters()
    {
        var previousFilter = CandidateFilter;
        var previousWasAll = IsAllFilter(previousFilter);
        var previousImport = ImportCandidate;

        _suppressFilterRefresh = true;
        CandidateFilterNames.Clear();
        CandidateFilterNames.Add(Loc["Filter_All"]);
        foreach (var candidate in KnownCandidates)
        {
            CandidateFilterNames.Add(candidate);
        }

        ImportCandidateNames.Clear();
        foreach (var candidate in KnownCandidates)
        {
            ImportCandidateNames.Add(candidate);
        }

        CandidateFilter = previousWasAll || string.IsNullOrWhiteSpace(previousFilter)
            ? Loc["Filter_All"]
            : previousFilter;

        ImportCandidate = KnownCandidates.Contains(previousImport, StringComparer.OrdinalIgnoreCase)
            ? previousImport
            : "java";
        _suppressFilterRefresh = false;
    }

    private bool IsAllFilter(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || string.Equals(value, Loc["Filter_All"], StringComparison.Ordinal)
        || string.Equals(value, "(all)", StringComparison.Ordinal)
        || string.Equals(value, "(全部)", StringComparison.Ordinal);

    private static bool HasConfiguredDefault(AppConfig config, string candidateName)
    {
        if (config.DefaultVersions.TryGetValue(candidateName, out var configuredAlias) &&
            !string.IsNullOrWhiteSpace(configuredAlias))
        {
            return true;
        }

        return string.Equals(candidateName, "java", StringComparison.OrdinalIgnoreCase)
               && !string.IsNullOrWhiteSpace(config.DefaultJavaAlias);
    }

    private static bool IsConfiguredDefault(AppConfig config, InstalledSdkVersion version)
    {
        if (config.DefaultVersions.TryGetValue(version.CandidateName, out var configuredAlias) &&
            !string.IsNullOrWhiteSpace(configuredAlias))
        {
            return string.Equals(configuredAlias, version.Alias, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(version.CandidateName, "java", StringComparison.OrdinalIgnoreCase)
               && !string.IsNullOrWhiteSpace(config.DefaultJavaAlias)
               && string.Equals(config.DefaultJavaAlias, version.Alias, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEnvironmentDefault(Dictionary<string, string> environmentHomes, InstalledSdkVersion version)
    {
        if (!environmentHomes.TryGetValue(version.CandidateName, out var environmentHome) ||
            string.IsNullOrWhiteSpace(environmentHome) ||
            string.IsNullOrWhiteSpace(version.HomeDirectory))
        {
            return false;
        }

        try
        {
            var left = Path.GetFullPath(environmentHome.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var right = Path.GetFullPath(version.HomeDirectory.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private bool CanRefresh() => IsIdle;

    private bool CanImport() => IsIdle;

    private bool CanMutateSelection() => IsIdle && SelectedInstalled is not null;

    protected override void OnBusyStateChanged() => NotifyActionCommands();

    private void NotifyActionCommands()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        ImportLocalCommand.NotifyCanExecuteChanged();
        SetDefaultCommand.NotifyCanExecuteChanged();
        UninstallCommand.NotifyCanExecuteChanged();
        CopyHomeCommand.NotifyCanExecuteChanged();
        ExportUseScriptCommand.NotifyCanExecuteChanged();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RebuildCandidateFilters();
        if (!_disposed)
        {
            _ = RefreshAsync();
        }
    }

    public override Task InitializeAsync(CancellationToken cancellationToken) => RefreshAsync();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Loc.LanguageChanged -= OnLanguageChanged;
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _disposed = true;
    }
}
