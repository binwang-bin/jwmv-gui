using Jwmv.Gui.Localization;

namespace Jwmv.Gui.Services;

public sealed class GuiBusyService
{
    private readonly LocalizationService _localization;
    private int _depth;

    public GuiBusyService(LocalizationService localization)
    {
        _localization = localization;
        StatusMessage = _localization["Status_Ready"];
        _localization.LanguageChanged += (_, _) =>
        {
            if (!IsBusy)
            {
                StatusMessage = _localization["Status_Ready"];
                Changed?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    public bool IsBusy => Volatile.Read(ref _depth) > 0;

    public string StatusMessage { get; private set; }

    public double ProgressPercentage { get; private set; }

    public bool IsProgressIndeterminate { get; private set; } = true;

    public event EventHandler? Changed;

    public IDisposable Begin(string statusMessage)
    {
        Interlocked.Increment(ref _depth);
        StatusMessage = statusMessage;
        ProgressPercentage = 0;
        IsProgressIndeterminate = true;
        Changed?.Invoke(this, EventArgs.Empty);
        return new Scope(this);
    }

    public void Report(string statusMessage, double percentage)
    {
        StatusMessage = statusMessage;
        ProgressPercentage = Math.Clamp(percentage, 0, 100);
        IsProgressIndeterminate = percentage <= 0;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void End()
    {
        if (Interlocked.Decrement(ref _depth) <= 0)
        {
            Volatile.Write(ref _depth, 0);
            StatusMessage = _localization["Status_Ready"];
            ProgressPercentage = 0;
            IsProgressIndeterminate = true;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Scope(GuiBusyService owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.End();
            }
        }
    }
}
