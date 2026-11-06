using MyPlanner.Models;

namespace MyPlanner.Services;

/// <summary>
/// Планировщик напоминаний. Всё состояние (когда сигнал последний раз звучал, до какого времени отложено,
/// было ли предупреждение) хранится в базе, поэтому после перезагрузки Windows оно восстанавливается.
/// </summary>
public sealed class ReminderService
{
    /// <summary>Источник времени (заменяется в тестах).</summary>
    public static Func<DateTime> Clock { get; set; } = () => DateTime.Now;

    /// <summary>Пора показать предупреждение-капельку.</summary>
    public event Action<List<TaskItem>>? PreWarning;
    /// <summary>Наступило время события (или очередной повтор сигнала).</summary>
    public event Action<List<TaskItem>>? Due;
    /// <summary>События, пропущенные, пока программа не работала или компьютер спал (одно общее уведомление).</summary>
    public event Action<List<TaskItem>>? Missed;

    /// <summary>Когда должен прозвучать следующий сигнал по задаче.</summary>
    public DateTime NextSignal(TaskItem t)
    {
        if (t.SnoozeUntil.HasValue) return t.SnoozeUntil.Value;
        if (t.LastSignal == null) return t.Due;
        return t.LastSignal.Value.AddMinutes(Svc.Settings.RepeatMinutes);
    }

    /// <param name="catchUp">true при запуске и выходе из сна: пропущенное сообщается одним уведомлением.</param>
    public void Tick(bool catchUp = false)
    {
        var now = Clock();
        var warn = new List<TaskItem>();
        var due = new List<TaskItem>();
        var missed = new List<TaskItem>();
        var gap = TimeSpan.FromMinutes(3);

        foreach (var t in Svc.Db.LoadAll())
        {
            if (t.IsDone || t.IsArchived || !t.RemindEnabled) continue;

            int w = Svc.Settings.EffectiveWarnMinutes(t);
            if (w > 0 && !t.Warned && now >= t.Due.AddMinutes(-w))
            {
                t.Warned = true;
                Svc.Db.Update(t);
                if (now < t.Due) warn.Add(t);
            }

            var next = NextSignal(t);
            if (next > now) continue;

            bool wasAway = catchUp && now - next > gap;
            t.LastSignal = now;
            t.SnoozeUntil = null;
            t.Signaled = true;
            t.Warned = true;
            Svc.Db.Update(t);
            if (wasAway) missed.Add(t); else due.Add(t);
        }

        if (warn.Count > 0) PreWarning?.Invoke(warn);
        if (missed.Count > 0) Missed?.Invoke(missed);
        if (due.Count > 0) Due?.Invoke(due);
        if (warn.Count > 0 || missed.Count > 0 || due.Count > 0) Svc.RaiseChanged();
    }
}
