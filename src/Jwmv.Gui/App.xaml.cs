using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Jwmv.Cli;
using Jwmv.Gui.Localization;
using Jwmv.Gui.Services;
using Jwmv.Gui.ViewModels;
using Jwmv.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Jwmv.Gui;

public partial class App : Application
{
    private const int AttachParentProcess = -1;

    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        if (CliApp.ShouldRunAsCli(e.Args))
        {
            RunCliAndExit(e.Args);
            return;
        }

        base.OnStartup(e);
        HookGlobalExceptionHandlers();

        var services = new ServiceCollection();
        services.AddJwmvInfrastructure();
        services.AddSingleton(sp => new LocalizationService(sp.GetRequiredService<JwmvPaths>().RootDirectory));
        services.AddSingleton<GuiBusyService>();
        services.AddSingleton<DoctorDiagnosticsService>();
        services.AddSingleton<OverviewViewModel>();
        services.AddSingleton<CatalogViewModel>();
        services.AddSingleton<InstalledViewModel>();
        services.AddSingleton<DoctorViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        _services = services.BuildServiceProvider();

        var localization = _services.GetRequiredService<LocalizationService>();
        Resources["Loc"] = localization;

        var mainWindow = _services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();

        var viewModel = _services.GetRequiredService<MainViewModel>();
        await viewModel.InitializeAsync(CancellationToken.None);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private void HookGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ShowFatalError(e.Exception);
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            ShowFatalError(exception);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        ShowFatalError(e.Exception.GetBaseException());
    }

    private static void ShowFatalError(Exception exception)
    {
        try
        {
            var dispatcher = Current?.Dispatcher;
            if (dispatcher is null)
            {
                MessageBox.Show(exception.Message, "jwmv", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (dispatcher.CheckAccess())
            {
                MessageBox.Show(exception.Message, "jwmv", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                dispatcher.Invoke(() =>
                    MessageBox.Show(exception.Message, "jwmv", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }
        catch
        {
            // Last-resort: avoid secondary crash while reporting.
        }
    }

    private void RunCliAndExit(string[] args)
    {
        // WinExe has no console by default; attach to the parent terminal when launched as `jwmv list`.
        _ = AttachConsole(AttachParentProcess);
        try
        {
            // Refresh System.Console writers after attaching so Spectre output is visible.
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
        catch (IOException)
        {
            // No parent console (rare). CLI still runs; output may be discarded.
        }

        var exitCode = CliApp.Run(CliApp.NormalizeArgs(args));
        Shutdown(exitCode);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);
}
