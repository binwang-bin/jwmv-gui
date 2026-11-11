using System.Diagnostics;
using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jwmv.Core.Abstractions;
using Jwmv.Core.Models;
using Jwmv.Gui.Localization;
using Jwmv.Gui.Services;
using Jwmv.Infrastructure;

namespace Jwmv.Gui.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly ISdkVersionManager _sdkManager;
    private readonly IJavaVersionManager _javaManager;
    private readonly JwmvPaths _paths;
    private bool _disposed;

    [ObservableProperty]
    private string? _rootDirectory;

    [ObservableProperty]
    private string? _configFilePath;

    [ObservableProperty]
    private string? _preferredDistributionAlias;

    [ObservableProperty]
    private int _catalogRefreshHours;

    [ObservableProperty]
    private bool _autoEnvEnabled;

    [ObservableProperty]
    private string? _defaultShell;

    [ObservableProperty]
    private string? _defaultJavaAlias;

    [ObservableProperty]
    private string? _defaultVersionsText;

    [ObservableProperty]
    private bool _flushArchives;

    [ObservableProperty]
    private bool _flushTemp;

    [ObservableProperty]
    private bool _flushCatalog;

    [ObservableProperty]
    private string? _aboutText;

    [ObservableProperty]
    private bool _isEnglishSelected;

    [ObservableProperty]
    private bool _isChineseSelected;

    public SettingsViewModel(
        ISdkVersionManager sdkManager,
        IJavaVersionManager javaManager,
        JwmvPaths paths,
        GuiBusyService busyService,
        LocalizationService localization)
        : base(busyService, localization)
    {
        _sdkManager = sdkManager;
        _javaManager = javaManager;
        _paths = paths;
        SyncLanguageSelection();
        localization.LanguageChanged += OnLanguageChanged;
    }

    [RelayCommand(CanExecute = nameof(CanRunIdle))]
    private async Task RefreshAsync()
    {
        await RunBusyAsync(Loc["Settings_Loading"], async cancellationToken =>
        {
            var config = await _sdkManager.GetConfigAsync(cancellationToken);
            RootDirectory = _paths.RootDirectory;
            ConfigFilePath = _paths.ConfigFilePath;
            PreferredDistributionAlias = config.PreferredDistributionAlias;
            CatalogRefreshHours = config.CatalogRefreshHours;
            AutoEnvEnabled = config.AutoEnvEnabled;
            DefaultShell = config.DefaultShell;
            DefaultJavaAlias = config.DefaultJavaAlias;
            SyncLanguageSelection();
            DefaultVersionsText = config.DefaultVersions.Count == 0
                ? Loc["Common_None"]
                : string.Join(Environment.NewLine, config.DefaultVersions.Select(pair => $"{pair.Key}={pair.Value}"));

            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "dev";
            AboutText = Loc.Format("Settings_AboutText", version, _paths.RootDirectory);
            InfoMessage = Loc["Settings_Loaded"];
        }, CancellationToken.None);

        NotifyActionCommands();
    }

    [RelayCommand]
    private void SetLanguage(string code)
    {
        Loc.SetLanguageFromCode(code);
        SyncLanguageSelection();
        var label = Loc.Language == UiLanguage.Chinese ? Loc["Language_Chinese"] : Loc["Language_English"];
        InfoMessage = Loc.Format("Settings_LanguageChanged", label);
        AboutText = Loc.Format(
            "Settings_AboutText",
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "dev",
            _paths.RootDirectory);
    }

    [RelayCommand(CanExecute = nameof(CanRunIdle))]
    private async Task RefreshCatalogAsync()
    {
        await RunBusyAsync(Loc["Settings_RefreshingCatalog"], async cancellationToken =>
        {
            await _sdkManager.RefreshCatalogAsync(cancellationToken);
            InfoMessage = Loc["Settings_CatalogRefreshed"];
        }, CancellationToken.None);

        NotifyActionCommands();
    }

    [RelayCommand(CanExecute = nameof(CanFlush))]
    private async Task FlushAsync()
    {
        if (!FlushArchives && !FlushTemp && !FlushCatalog)
        {
            ErrorMessage = Loc["Settings_FlushSelectOne"];
            return;
        }

        await RunBusyAsync(Loc["Settings_Flushing"], async cancellationToken =>
        {
            await _javaManager.FlushAsync(new FlushRequest
            {
                IncludeArchives = FlushArchives,
                IncludeTemp = FlushTemp,
                IncludeCatalog = FlushCatalog
            }, cancellationToken);
            InfoMessage = Loc["Settings_FlushDone"];
        }, CancellationToken.None);

        NotifyActionCommands();
    }

    [RelayCommand(CanExecute = nameof(CanRunIdle))]
    private void UninstallProduct()
    {
        ClearMessages();
        if (!ProductUninstaller.TryResolve(out var fileName, out var arguments, out _))
        {
            ErrorMessage = Loc["Settings_UninstallNotFound"];
            return;
        }

        var confirm = MessageBox.Show(
            Loc["Settings_ConfirmUninstall"],
            Loc["Settings_ConfirmUninstallTitle"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            ErrorMessage = Loc.Format("Settings_UninstallFailed", exception.Message);
            return;
        }

        // Close the GUI so Inno can replace/delete files under the install directory.
        Application.Current?.Shutdown();
    }

    partial void OnFlushArchivesChanged(bool value) => NotifyActionCommands();

    partial void OnFlushTempChanged(bool value) => NotifyActionCommands();

    partial void OnFlushCatalogChanged(bool value) => NotifyActionCommands();

    private bool CanRunIdle() => IsIdle;

    private bool CanFlush() => IsIdle && (FlushArchives || FlushTemp || FlushCatalog);

    protected override void OnBusyStateChanged() => NotifyActionCommands();

    private void NotifyActionCommands()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        RefreshCatalogCommand.NotifyCanExecuteChanged();
        FlushCommand.NotifyCanExecuteChanged();
        UninstallProductCommand.NotifyCanExecuteChanged();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        SyncLanguageSelection();
        AboutText = Loc.Format(
            "Settings_AboutText",
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "dev",
            _paths.RootDirectory);
    }

    private void SyncLanguageSelection()
    {
        IsEnglishSelected = Loc.Language == UiLanguage.English;
        IsChineseSelected = Loc.Language == UiLanguage.Chinese;
    }

    public override Task InitializeAsync(CancellationToken cancellationToken) => RefreshAsync();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Loc.LanguageChanged -= OnLanguageChanged;
        _disposed = true;
    }
}
