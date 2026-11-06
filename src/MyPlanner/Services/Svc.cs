using MyPlanner.Models;
using MyPlanner.Views;

namespace MyPlanner.Services;

/// <summary>Общие сервисы приложения.</summary>
public static class Svc
{
    public static Database Db { get; set; } = null!;
    public static AppSettings Settings { get; set; } = null!;
    public static ReminderService Reminders { get; set; } = null!;
    public static WeatherService Weather { get; set; } = null!;
    public static TrayService Tray { get; set; } = null!;
    public static SpeechService Speech { get; } = new();
    public static MainWindow? Main { get; set; }

    /// <summary>Данные изменились (задачи/настройки): интерфейс должен обновиться.</summary>
    public static event Action? DataChanged;
    public static void RaiseChanged() => DataChanged?.Invoke();
}

/// <summary>Операции над задачами. Все изменения сразу сохраняются в базу.</summary>
public static class TaskOps
{
    public static void Save(TaskItem t)
    {
        if (t.Id == 0)
        {
            Svc.Db.Insert(t);
        }
        else
        {
            var old = Svc.Db.Get(t.Id);
            if (old != null && (old.Due != t.Due || old.WarnMinutes != t.WarnMinutes))
            {
                // перенос задачи: капелька и напоминания начинаются заново
                t.Warned = false;
                t.Signaled = false;
                t.LastSignal = null;
                t.SnoozeUntil = null;
            }
            Svc.Db.Update(t);
        }
        Svc.RaiseChanged();
    }

    public static void Complete(TaskItem t)
    {
        var now = DateTime.Now;
        var next = Recurrence.Next(t, now);
        if (next != null)
        {
            // повторяющаяся задача: в историю уходит выполненный экземпляр, задача переносится на следующую дату
            var done = t.Clone();
            done.Id = 0;
            done.IsDone = true;
            done.CompletedAt = now;
            done.Repeat = RepeatKind.None;
            done.SnoozeUntil = null;
            Svc.Db.Insert(done);

            t.Due = next.Value;
            t.Warned = false;
            t.Signaled = false;
            t.LastSignal = null;
            t.SnoozeUntil = null;
            Svc.Db.Update(t);
        }
        else
        {
            t.IsDone = true;
            t.CompletedAt = now;
            t.SnoozeUntil = null;
            Svc.Db.Update(t);
        }
        Svc.RaiseChanged();
    }

    public static void Snooze(TaskItem t, int minutes)
    {
        t.SnoozeUntil = DateTime.Now.AddMinutes(minutes);
        Svc.Db.Update(t);
        Svc.RaiseChanged();
    }

    /// <summary>Удаление: задача остаётся в истории.</summary>
    public static void Archive(TaskItem t)
    {
        t.IsArchived = true;
        t.SnoozeUntil = null;
        Svc.Db.Update(t);
        Svc.RaiseChanged();
    }

    public static void Restore(TaskItem t)
    {
        t.IsArchived = false;
        t.IsDone = false;
        t.CompletedAt = null;
        if (t.Due < DateTime.Now)
        {
            t.Warned = false;
            t.Signaled = false;
            t.LastSignal = null;
        }
        Svc.Db.Update(t);
        Svc.RaiseChanged();
    }

    public static void DeleteForever(TaskItem t)
    {
        Svc.Db.Delete(t.Id);
        Svc.RaiseChanged();
    }

    /// <summary>Перенос на другую дату с сохранением времени.</summary>
    public static void MoveToDate(TaskItem t, DateTime date)
    {
        t.Due = date.Date + t.Due.TimeOfDay;
        Save(t);
    }
}
