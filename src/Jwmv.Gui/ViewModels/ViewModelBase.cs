using CommunityToolkit.Mvvm.ComponentModel;
using Jwmv.Gui.Localization;
using Jwmv.Gui.Services;

namespace Jwmv.Gui.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    private string? _errorMessage;
    private string? _infoMessage;

    protected ViewModelBase(GuiBusyService busyService, LocalizationService localization)
    {
        BusyService = busyService;
        Loc = localization;
        BusyService.Changed += OnBusyChanged;
    }

    protected GuiBusyService BusyService { get; }

    protected LocalizationService Loc { get; }

    public LocalizationService Strings => Loc;

    public bool IsBusy => BusyService.IsBusy;

    public bool IsIdle => !BusyService.IsBusy;

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public string? InfoMessage
    {
        get => _infoMessage;
        set => SetProperty(ref _infoMessage, value);
    }

    public virtual Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected void ClearMessages()
    {
        ErrorMessage = null;
        InfoMessage = null;
    }

    protected virtual void OnBusyStateChanged()
    {
    }

    protected async Task RunBusyAsync(string status, Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        ClearMessages();
        using (BusyService.Begin(status))
        {
            try
            {
                await action(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                InfoMessage = Loc["Status_Cancelled"];
            }
            catch (Exception exception)
            {
                ErrorMessage = exception.Message;
            }
        }
    }

    protected async Task<T?> RunBusyAsync<T>(string status, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        ClearMessages();
        using (BusyService.Begin(status))
        {
            try
            {
                return await action(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                InfoMessage = Loc["Status_Cancelled"];
                return default;
            }
            catch (Exception exception)
            {
                ErrorMessage = exception.Message;
                return default;
            }
        }
    }

    private void OnBusyChanged(object? sender, EventArgs e)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            RaiseBusyProperties();
            return;
        }

        _ = dispatcher.InvokeAsync(RaiseBusyProperties);
    }

    private void RaiseBusyProperties()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(IsIdle));
        OnBusyStateChanged();
    }
}
