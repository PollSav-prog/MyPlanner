using System.Windows;
using System.Windows.Threading;
using Microsoft.Toolkit.Uwp.Notifications;
using MyPlanner.Models;
using MyPlanner.Views;

namespace MyPlanner.Services;

/// <summary>
/// Связывает ReminderService с интерфейсом: звук, уведомления Windows, карточки напоминаний.
/// Звук после выполнения задачи не воспроизводится: выполненные задачи не попадают в Tick.
/// </summary>
public static class NotificationCoordinator
{
    private static readonly Dictionary<long, ReminderCardWindow> OpenCards = new();
    private static DateTime _warnPostponedUntil = DateTime.MinValue;

    public static void Init()
    {
        Svc.Reminders.PreWarning += list => Ui(() => OnWarning(list));
        Svc.Reminders.Due += list => Ui(() => OnDue(list));
        Svc.Reminders.Missed += list => Ui(() => OnMissed(list));
        Svc.DataChanged += CloseCardsForFinishedTasks;
        ToastNotificationManagerCompat.OnActivated += OnToastActivated;
    }

    private static void Ui(Action a) => Application.Current.Dispatcher.InvokeAsync(a);

    private static bool MainVisible =>
        Svc.Main is { IsVisible: true } w && w.WindowState != WindowState.Minimized;

    // ---------- Предупреждение (капелька) ----------
    private static void OnWarning(List<TaskItem> list)
    {
        if (DateTime.Now < _warnPostponedUntil) return;
        if (MainVisible)
            ShowWarningCard(list);
        else
            foreach (var t in list) ToastWarn(t);
    }

    public static void ShowWarningCard(List<TaskItem> list)
    {
        if (list.Count == 0) return;
        var win = new ReminderCardWindow(ReminderCardMode.Warning, list);
        win.Show();
    }

    public static void PostponeWarnings(int minutes) => _warnPostponedUntil = DateTime.Now.AddMinutes(minutes);

    // ---------- Основное напоминание ----------
    private static void OnDue(List<TaskItem> list)
    {
        PlaySignal();
        int shown = 0;
        foreach (var t in list)
        {
            ToastDue(t);
            if (OpenCards.ContainsKey(t.Id)) continue;
            if (shown++ >= 3) continue;
            var win = new ReminderCardWindow(ReminderCardMode.Due, new List<TaskItem> { t });
            OpenCards[t.Id] = win;
            win.Closed += (_, _) => OpenCards.Remove(t.Id);
            win.Show();
        }
    }

    private static void OnMissed(List<TaskItem> list)
    {
        PlaySignal();
        var summary = Loc.I.F("toast_missed", list.Count) + "\n" +
                      string.Join("\n", list.Take(5).Select(t => $"• {t.Due:t} {t.Title}"));
        try
        {
            new ToastContentBuilder()
                .AddArgument("action", "open")
                .AddText(Loc.I["toast_missed_title"])
                .AddText(summary)
                .AddAudio(new Uri("ms-winsoundevent:Notification.Default"), silent: true)
                .Show(toast => { toast.Tag = "missed"; toast.Group = "due"; });
        }
        catch (Exception ex) { Logger.Write("Toast failed", ex); }
        Svc.Main?.ShowFromTray();
    }

    public static void PlaySignal()
    {
        var s = Svc.Settings;
        if (!s.SoundEnabled || s.IsQuietNow(DateTime.Now)) return;
        SoundService.Play(s.SoundId, s.Volume);
    }

    // ---------- Уведомления Windows ----------
    private static void ToastDue(TaskItem t)
    {
        try
        {
            new ToastContentBuilder()
                .AddArgument("action", "open").AddArgument("id", t.Id.ToString())
                .AddText(Loc.I["toast_due_title"])
                .AddText(t.Title)
                .AddText(t.Due.ToString("t", Loc.I.Culture))
                .AddButton(new ToastButton().SetContent(Loc.I["btn_done"])
                    .AddArgument("action", "done").AddArgument("id", t.Id.ToString()).SetBackgroundActivation())
                .AddButton(new ToastButton().SetContent(Loc.I["btn_snooze_10"])
                    .AddArgument("action", "snooze").AddArgument("id", t.Id.ToString()).SetBackgroundActivation())
                .AddAudio(new Uri("ms-winsoundevent:Notification.Default"), silent: true)
                .Show(toast => { toast.Tag = "t" + t.Id; toast.Group = "due"; });
        }
        catch (Exception ex) { Logger.Write("Toast failed", ex); }
    }

    private static void ToastWarn(TaskItem t)
    {
        try
        {
            new ToastContentBuilder()
                .AddArgument("action", "open").AddArgument("id", t.Id.ToString())
                .AddText(Loc.I["droplet_title"])
                .AddText(Loc.I.F("droplet_name", t.Title))
                .AddText(Loc.I.F("droplet_time", t.Due.ToString("t", Loc.I.Culture)))
                .AddAudio(new Uri("ms-winsoundevent:Notification.Default"), silent: true)
                .Show(toast => { toast.Tag = "w" + t.Id; toast.Group = "warn"; });
        }
        catch (Exception ex) { Logger.Write("Toast failed", ex); }
    }

    private static void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        var args = ToastArguments.Parse(e.Argument);
        Ui(() =>
        {
            args.TryGetValue("action", out var action);
            long id = args.TryGetValue("id", out var s) && long.TryParse(s, out var l) ? l : 0;
            var t = id > 0 ? Svc.Db.Get(id) : null;
            switch (action)
            {
                case "done" when t != null: TaskOps.Complete(t); break;
                case "snooze" when t != null: TaskOps.Snooze(t, 10); break;
                default:
                    Svc.Main?.ShowFromTray();
                    if (t != null) Svc.Main?.ViewModel.SelectDate(t.Due.Date, t);
                    break;
            }
        });
    }

    private static void CloseCardsForFinishedTasks()
    {
        foreach (var kv in OpenCards.ToList())
        {
            var t = Svc.Db.Get(kv.Key);
            bool pending = t != null && !t.IsDone && !t.IsArchived && t.RemindEnabled && t.SnoozeUntil == null;
            if (!pending)
            {
                kv.Value.Close();
                try { ToastNotificationManagerCompat.History.Remove("t" + kv.Key, "due"); } catch { }
            }
        }
    }
}
