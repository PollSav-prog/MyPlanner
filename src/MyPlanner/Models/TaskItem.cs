using System.Windows;
using System.Windows.Media;
using MyPlanner.Services;

namespace MyPlanner.Models;

public enum RepeatKind
{
    None = 0,
    Daily = 1,
    Weekdays = 2,     // по выбранным дням недели
    Weekly = 3,
    Monthly = 4,
    Yearly = 5,
    EveryNDays = 6
}

public class TaskItem
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime Due { get; set; } = DateTime.Today.AddHours(9);
    /// <summary>0 — низкий, 1 — обычный, 2 — высокий</summary>
    public int Priority { get; set; } = 1;
    public string Category { get; set; } = "";
    public bool IsDone { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? CompletedAt { get; set; }

    public bool RemindEnabled { get; set; } = true;
    /// <summary>-1 — как в настройках, 0 — без предупреждения, иначе минуты до события</summary>
    public int WarnMinutes { get; set; } = -1;

    public RepeatKind Repeat { get; set; } = RepeatKind.None;
    public int RepeatInterval { get; set; } = 1;
    /// <summary>Битовая маска дней: Пн = 1, Вт = 2 ... Вс = 64</summary>
    public int RepeatDays { get; set; }

    public DateTime? SnoozeUntil { get; set; }
    public DateTime? LastSignal { get; set; }
    public bool Warned { get; set; }
    public bool Signaled { get; set; }

    // ---------- Отображение ----------
    public bool IsOverdue => !IsDone && Due < DateTime.Now;
    public string TimeText => Due.ToString("t", Loc.I.Culture);
    public string DateTimeText => Due.ToString("g", Loc.I.Culture);
    public string DoneMark => IsDone ? "✓" : "";
    public double RowOpacity => IsDone ? 0.55 : 1.0;
    public TextDecorationCollection? Decor => IsDone ? TextDecorations.Strikethrough : null;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public bool HasCategory => !string.IsNullOrWhiteSpace(Category);
    public bool IsRepeating => Repeat != RepeatKind.None;
    public Visibility OverdueVisibility => IsOverdue ? Visibility.Visible : Visibility.Collapsed;

    public Brush PriorityBrush
    {
        get
        {
            string key = Priority switch { 0 => "PrioLow", 2 => "PrioHigh", _ => "PrioNormal" };
            return (Brush)Application.Current.FindResource(key);
        }
    }

    public string PriorityText => Loc.I[Priority switch { 0 => "prio_low", 2 => "prio_high", _ => "prio_normal" }];

    public string RepeatText => Recurrence.Describe(this);

    public TaskItem Clone() => (TaskItem)MemberwiseClone();
}
