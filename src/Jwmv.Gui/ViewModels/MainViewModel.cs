using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jwmv.Gui.Localization;
using Jwmv.Gui.Services;

namespace Jwmv.Gui.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly GuiBusyService _busyService;
    private readonly OverviewViewModel _overview;
    private readonly CatalogViewModel _catalog;
    private readonly InstalledViewModel _installed;
    private readonly DoctorViewModel _doctor;
    private readonly SettingsViewModel _settings;
    private CancellationTokenSource? _navigationCts;
    private bool _disposed;

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private string _statusMessage;

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private bool _isProgressIndeterminate = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _selectedNav = "Overview";

    public MainViewModel(
        GuiBusyService busyService,
        LocalizationService localization,
        OverviewViewModel overview,
        CatalogViewModel catalog,
        InstalledViewModel installed,
        DoctorViewModel doctor,
        SettingsViewModel settings)
    {
        _busyService = busyService;
        Loc = localization;
        _overview = overview;
        _catalog = catalog;
        _installed = installed;
        _doctor = doctor;
        _settings = settings;
        _statusMessage = localization["Status_Ready"];
        _busyService.Changed += OnBusyChanged;
        CurrentPage = _overview;
    }

    public LocalizationService Loc { get; }

    public bool IsIdle => !IsBusy;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _overview.InitializeAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task NavigateAsync(string page)
    {
        SelectedNav = page;
        CurrentPage = page switch
        {
            "Catalog" => _catalog,
            "Installed" => _installed,
            "Doctor" => _doctor,
            "Settings" => _settings,
            _ => _overview
        };

        _navigationCts?.Cancel();
        _navigationCts?.Dispose();
        _navigationCts = new CancellationTokenSource();
        var token = _navigationCts.Token;

        if (CurrentPage is ViewModelBase viewModel)
        {
            try
            {
                await viewModel.InitializeAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Superseded by a newer navigation.
            }
        }
    }

    private void OnBusyChanged(object? sender, EventArgs e)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            ApplyBusyState();
            return;
        }

        if (dispatcher.CheckAccess())
        {
            ApplyBusyState();
        }
        else
        {
            _ = dispatcher.InvokeAsync(ApplyBusyState);
        }
    }

    private void ApplyBusyState()
    {
        IsBusy = _busyService.IsBusy;
        OnPropertyChanged(nameof(IsIdle));
        StatusMessage = _busyService.StatusMessage;
        ProgressPercentage = _busyService.ProgressPercentage;
        IsProgressIndeterminate = _busyService.IsProgressIndeterminate;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _busyService.Changed -= OnBusyChanged;
        _navigationCts?.Cancel();
        _navigationCts?.Dispose();
        _disposed = true;
    }
}
