using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jwmv.Core.Abstractions;
using Jwmv.Core.Models;
using Jwmv.Gui.Localization;
using Jwmv.Gui.Services;

namespace Jwmv.Gui.ViewModels;

public sealed partial class OverviewViewModel : ViewModelBase, IDisposable
{
    private readonly ISdkVersionManager _manager;
    private CancellationTokenSource? _refreshCts;
    private bool _disposed;

    [ObservableProperty]
    private bool _isSelectionsEmpty = true;

    [ObservableProperty]
    private bool _isCandidatesEmpty = true;

    public OverviewViewModel(ISdkVersionManager manager, GuiBusyService busyService, LocalizationService localization)
        : base(busyService, localization)
    {
        _manager = manager;
    }

    public ObservableCollection<ActiveSdkSelection> CurrentSelections { get; } = [];

    public ObservableCollection<SdkCandidate> Candidates { get; } = [];

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = new CancellationTokenSource();
        var token = _refreshCts.Token;

        await RunBusyAsync(Loc["Overview_Loading"], async cancellationToken =>
        {
            var candidates = await _manager.ListCandidatesAsync(null, cancellationToken);
            var current = await _manager.ResolveAllCurrentAsync(null, cancellationToken);

            Candidates.Clear();
            foreach (var candidate in candidates)
            {
                Candidates.Add(candidate);
            }

            CurrentSelections.Clear();
            foreach (var selection in current.Where(item => item.IsResolved))
            {
                CurrentSelections.Add(selection);
            }

            IsSelectionsEmpty = CurrentSelections.Count == 0;
            IsCandidatesEmpty = Candidates.Count == 0;

            InfoMessage = Loc.Format(
                "Overview_LoadedResolved",
                CurrentSelections.Count,
                candidates.Count);
        }, token);

        RefreshCommand.NotifyCanExecuteChanged();
    }

    private bool CanRefresh() => IsIdle;

    protected override void OnBusyStateChanged() => RefreshCommand.NotifyCanExecuteChanged();

    public override Task InitializeAsync(CancellationToken cancellationToken) => RefreshAsync();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _disposed = true;
    }
}
