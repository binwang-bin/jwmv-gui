using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jwmv.Core.Abstractions;
using Jwmv.Core.Models;
using Jwmv.Gui.Localization;
using Jwmv.Gui.Services;

namespace Jwmv.Gui.ViewModels;

public sealed partial class CatalogViewModel : ViewModelBase, IDisposable
{
    private readonly ISdkVersionManager _manager;
    private CancellationTokenSource? _installCts;
    private CancellationTokenSource? _loadCts;
    private bool _disposed;
    private bool _suppressCandidateReload;

    [ObservableProperty]
    private string _selectedCandidate = "java";

    [ObservableProperty]
    private string? _versionFilter;

    [ObservableProperty]
    private bool _forceRefresh;

    [ObservableProperty]
    private bool _setAsDefault;

    [ObservableProperty]
    private bool _allowUnverified;

    [ObservableProperty]
    private SdkPackage? _selectedPackage;

    [ObservableProperty]
    private string? _manualVersion;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private string _installButtonText = string.Empty;

    [ObservableProperty]
    private bool _isPackagesEmpty = true;

    public CatalogViewModel(ISdkVersionManager manager, GuiBusyService busyService, LocalizationService localization)
        : base(busyService, localization)
    {
        _manager = manager;
        InstallButtonText = Loc["Common_Install"];
        localization.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<string> CandidateNames { get; } = ["java", "gradle", "maven", "kotlin"];

    public ObservableCollection<SdkPackage> Packages { get; } = [];

    [RelayCommand(CanExecute = nameof(CanLoadCatalog))]
    private async Task LoadCatalogAsync()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        await RunBusyAsync(Loc.Format("Catalog_Loading", SelectedCandidate), async cancellationToken =>
        {
            var packages = await _manager.ListAvailableAsync(new SdkCatalogQuery
            {
                CandidateName = SelectedCandidate,
                VersionFilter = string.IsNullOrWhiteSpace(VersionFilter) ? null : VersionFilter,
                ForceRefresh = ForceRefresh
            }, cancellationToken);

            Packages.Clear();
            foreach (var package in packages)
            {
                Packages.Add(package);
            }

            IsPackagesEmpty = Packages.Count == 0;
            InfoMessage = Loc.Format("Catalog_Loaded", Packages.Count, SelectedCandidate);
        }, token);

        NotifyActionCommands();
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallSelectedAsync()
    {
        var version = SelectedPackage?.Alias ?? ManualVersion;
        if (string.IsNullOrWhiteSpace(version))
        {
            ErrorMessage = Loc["Catalog_SelectPackage"];
            return;
        }

        await InstallAsync(SelectedCandidate, version);
    }

    [RelayCommand]
    private void CancelInstall()
    {
        if (_installCts is null || _installCts.IsCancellationRequested)
        {
            InfoMessage = Loc["Catalog_NothingToCancel"];
            return;
        }

        _installCts.Cancel();
        InfoMessage = Loc["Catalog_CancelRequested"];
    }

    private async Task InstallAsync(string candidateName, string version)
    {
        _installCts?.Cancel();
        _installCts?.Dispose();
        _installCts = new CancellationTokenSource();
        var cancellationToken = _installCts.Token;

        IsInstalling = true;
        InstallButtonText = Loc["Catalog_InstallingButton"];
        NotifyActionCommands();

        var progress = new Progress<InstallProgressUpdate>(update =>
            BusyService.Report(update.Status, update.Percentage));

        try
        {
            var result = await RunBusyAsync(Loc.Format("Catalog_Installing", candidateName, version), async token =>
            {
                return await _manager.InstallAsync(new InstallSdkRequest
                {
                    CandidateName = candidateName,
                    Version = version,
                    SetAsDefault = SetAsDefault,
                    ForceCatalogRefresh = ForceRefresh,
                    AllowUnverified = AllowUnverified
                }, progress, token);
            }, cancellationToken);

            if (result is null)
            {
                return;
            }

            InfoMessage = result.AlreadyInstalled
                ? Loc.Format("Catalog_AlreadyInstalled", result.InstalledVersion.CandidateName, result.InstalledVersion.Alias)
                : Loc.Format("Catalog_Installed", result.InstalledVersion.CandidateName, result.InstalledVersion.Alias);

            if (result.DefaultWasUpdated)
            {
                InfoMessage += Loc["Catalog_DefaultUpdated"];
            }

            if (!string.IsNullOrWhiteSpace(result.IntegrityWarning))
            {
                InfoMessage += Environment.NewLine + result.IntegrityWarning;
            }
        }
        finally
        {
            IsInstalling = false;
            InstallButtonText = Loc["Common_Install"];
            NotifyActionCommands();
        }
    }

    partial void OnSelectedCandidateChanged(string value)
    {
        if (_suppressCandidateReload || _disposed)
        {
            return;
        }

        _ = LoadCatalogAsync();
    }

    partial void OnSelectedPackageChanged(SdkPackage? value) => NotifyActionCommands();

    partial void OnManualVersionChanged(string? value) => NotifyActionCommands();

    private bool CanLoadCatalog() => IsIdle;

    private bool CanInstall() =>
        IsIdle &&
        !IsInstalling &&
        (!string.IsNullOrWhiteSpace(SelectedPackage?.Alias) || !string.IsNullOrWhiteSpace(ManualVersion));

    protected override void OnBusyStateChanged() => NotifyActionCommands();

    private void NotifyActionCommands()
    {
        LoadCatalogCommand.NotifyCanExecuteChanged();
        InstallSelectedCommand.NotifyCanExecuteChanged();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        InstallButtonText = IsInstalling ? Loc["Catalog_InstallingButton"] : Loc["Common_Install"];
    }

    public override async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _suppressCandidateReload = true;
        try
        {
            await LoadCatalogAsync();
        }
        finally
        {
            _suppressCandidateReload = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Loc.LanguageChanged -= OnLanguageChanged;
        _installCts?.Dispose();
        _loadCts?.Dispose();
        _disposed = true;
    }
}
