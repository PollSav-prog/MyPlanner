using System.Windows;
using System.Windows.Threading;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using MyPlanner.Models;
using MyPlanner.Services;
using MyPlanner.Views;

namespace MyPlanner;

public partial class App : Application
{
    private const string Id = "MyPlanner_7f3a9c21";
    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandled;
        AppDomain.CurrentDomain.UnhandledException += (_, a) => Logger.Write("Fatal", a.ExceptionObject as Exception);

        // --- только один экземпляр: второй запуск просто показывает уже открытое окно ---
        _mutex = new Mutex(true, Id + "_mutex", out bool first);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Id + "_show");
        if (!first)
        {
            _showEvent.Set();
            Shutdown();
            return;
        }
        new Thread(() =>
        {
            while (_showEvent.WaitOne())
                Dispatcher.InvokeAsync(() => Svc.Main?.ShowFromTray());
        }) { IsBackground = true }.Start();

        try
        {
            StartApp(e);
        }
        catch (Exception ex)
        {
            Logger.Write("Startup failed", ex);
            MessageBox.Show("Не удалось запустить программу / Startup failed:\n\n" + ex.Message,
                "Мой планировщик", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void StartApp(StartupEventArgs e)
    {
        // --- база данных и настройки ---
        Svc.Db = new Database();
        Svc.Settings = new AppSettings(Svc.Db);
        Loc.I.Load();

        // --- язык: при первом запуске предлагаем выбрать ---
        if (string.IsNullOrEmpty(Svc.Settings.Language) || !Loc.I.IsSupported(Svc.Settings.Language))
        {
            ThemeService.Init();
            ThemeService.Apply("system");
            Loc.I.SetLanguage(Loc.Fallback);
            var suggested = Loc.I.DetectWindowsLanguage() ?? Loc.Fallback;
            var chosen = Dialogs.PickLanguageFirstRun(suggested);
            Svc.Settings.Language = chosen;
            Svc.Settings.SpeechLanguage = chosen;
        }
        else ThemeService.Init();

        Loc.I.SetLanguage(Svc.Settings.Language);
        ThemeService.Apply(Svc.Settings.Theme);

        // --- сервисы ---
        Svc.Reminders = new ReminderService();
        Svc.Weather = new WeatherService();
        NotificationCoordinator.Init();

        var main = new MainWindow();
        Svc.Main = main;
        MainWindow = main;

        Svc.Tray = new TrayService
        {
            OpenRequested = () => main.ShowFromTray(),
            AddRequested = () =>
            {
                main.ShowFromTray();
                main.ViewModel.AddCommand.Execute(null);
            },
            ExitRequested = ExitApp
        };

        bool minimized = e.Args.Contains("--minimized") || Svc.Settings.StartMinimized;
        if (!minimized) main.Show();

        // восстанавливаем состояние напоминаний после перезагрузки: пропущенное показываем одним уведомлением
        main.ViewModel.CatchUp();
        _ = Svc.Weather.RefreshAsync();

        // выход из сна / разблокировка — проверяем пропущенные события
        SystemEvents.PowerModeChanged += (_, a) =>
        {
            if (a.Mode == PowerModes.Resume)
                Dispatcher.InvokeAsync(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    main.ViewModel.CatchUp();
                    _ = Svc.Weather.RefreshAsync();
                });
        };
    }

    public void ExitApp()
    {
        if (Svc.Main != null)
        {
            Svc.Main.ExitRequested = true;
            Svc.Main.Close();
        }
        Svc.Tray?.Dispose();
        Shutdown();
    }

    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Write("Unhandled", e.Exception);
        e.Handled = true;
        try { Dialogs.Info(Loc.I["err_generic"]); } catch { }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Svc.Tray?.Dispose();
        try { _mutex?.ReleaseMutex(); } catch { }
        base.OnExit(e);
    }
}
