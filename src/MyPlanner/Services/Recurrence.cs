using System.Globalization;
using MyPlanner.Models;

namespace MyPlanner.Services;

/// <summary>Правила повторения задач.</summary>
public static class Recurrence
{
    public static int DayBit(DayOfWeek d) => 1 << (((int)d + 6) % 7); // Пн = бит 0

    /// <summary>Следующее вхождение строго после <paramref name="due"/> и позже <paramref name="now"/>.</summary>
    public static DateTime? Next(TaskItem t, DateTime now)
    {
        if (t.Repeat == RepeatKind.None) return null;
        var next = Step(t, t.Due);
        if (next == null) return null;
        int guard = 0;
        while (next <= now && guard++ < 5000)
        {
            next = Step(t, next.Value);
            if (next == null) return null;
        }
        return next;
    }

    private static DateTime? Step(TaskItem t, DateTime from)
    {
        switch (t.Repeat)
        {
            case RepeatKind.Daily: return from.AddDays(1);
            case RepeatKind.Weekly: return from.AddDays(7);
            case RepeatKind.Monthly: return from.AddMonths(1);
            case RepeatKind.Yearly: return from.AddYears(1);
            case RepeatKind.EveryNDays: return from.AddDays(Math.Max(1, t.RepeatInterval));
            case RepeatKind.Weekdays:
                if (t.RepeatDays == 0) return from.AddDays(1);
                for (int i = 1; i <= 7; i++)
                {
                    var d = from.AddDays(i);
                    if ((t.RepeatDays & DayBit(d.DayOfWeek)) != 0) return d;
                }
                return null;
            default: return null;
        }
    }

    public static string Describe(TaskItem t)
    {
        var loc = Loc.I;
        switch (t.Repeat)
        {
            case RepeatKind.Daily: return loc["rep_daily"];
            case RepeatKind.Weekly: return loc["rep_weekly"];
            case RepeatKind.Monthly: return loc["rep_monthly"];
            case RepeatKind.Yearly: return loc["rep_yearly"];
            case RepeatKind.EveryNDays: return loc.F("rep_every_n", Math.Max(1, t.RepeatInterval));
            case RepeatKind.Weekdays:
                var names = new List<string>();
                var fmt = loc.Culture.DateTimeFormat;
                for (int i = 0; i < 7; i++)
                {
                    if ((t.RepeatDays & (1 << i)) == 0) continue;
                    var dow = (DayOfWeek)((i + 1) % 7);
                    names.Add(fmt.GetAbbreviatedDayName(dow));
                }
                return string.Join(", ", names);
            default: return "";
        }
    }
}
