using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MyPlanner.Models;
using MyPlanner.Services;

namespace MyPlanner.Views;

/// <summary>Панель настройки повторения (используется в окне задачи и в отдельном окне «Повторение»).</summary>
public class RepeatPanel : StackPanel
{
    private readonly ComboBox _kind = new();
    private readonly TextBox _interval = new() { Width = 60, Text = "2", Margin = new Thickness(8, 0, 8, 0) };
    private readonly StackPanel _everyRow = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
    private readonly WrapPanel _days = new() { Margin = new Thickness(0, 8, 0, 0) };
    private readonly ToggleButton[] _dayButtons = new ToggleButton[7];

    private static readonly RepeatKind[] Kinds =
    {
        RepeatKind.None, RepeatKind.Daily, RepeatKind.Weekdays, RepeatKind.Weekly,
        RepeatKind.Monthly, RepeatKind.Yearly, RepeatKind.EveryNDays
    };

    private static readonly string[] KindKeys =
    {
        "rep_none", "rep_daily", "rep_weekdays", "rep_weekly", "rep_monthly", "rep_yearly", "rep_everyn"
    };

    public RepeatPanel()
    {
        foreach (var k in KindKeys) _kind.Items.Add(new ComboBoxItem { Content = Loc.I[k] });
        _kind.SelectedIndex = 0;
        _kind.SelectionChanged += (_, _) => UpdateVisibility();
        Children.Add(_kind);

        _everyRow.Children.Add(new TextBlock { Text = Loc.I["rep_every_label"], VerticalAlignment = VerticalAlignment.Center });
        _everyRow.Children.Add(_interval);
        _everyRow.Children.Add(new TextBlock { Text = Loc.I["rep_days_label"], VerticalAlignment = VerticalAlignment.Center });
        Children.Add(_everyRow);

        var fmt = Loc.I.Culture.DateTimeFormat;
        for (int i = 0; i < 7; i++)
        {
            var dow = (DayOfWeek)((i + 1) % 7);
            var name = fmt.GetAbbreviatedDayName(dow);
            var tb = new ToggleButton { Content = name, MinWidth = 42, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(6, 5, 6, 5) };
            _dayButtons[i] = tb;
            _days.Children.Add(tb);
        }
        Children.Add(_days);
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        var k = Kinds[Math.Max(0, _kind.SelectedIndex)];
        _everyRow.Visibility = k == RepeatKind.EveryNDays ? Visibility.Visible : Visibility.Collapsed;
        _days.Visibility = k == RepeatKind.Weekdays ? Visibility.Visible : Visibility.Collapsed;
    }

    public void Load(TaskItem t)
    {
        _kind.SelectedIndex = Math.Max(0, Array.IndexOf(Kinds, t.Repeat));
        _interval.Text = Math.Max(1, t.RepeatInterval == 0 ? 2 : t.RepeatInterval).ToString();
        for (int i = 0; i < 7; i++) _dayButtons[i].IsChecked = (t.RepeatDays & (1 << i)) != 0;
        UpdateVisibility();
    }

    /// <summary>Записывает выбор в задачу. Возвращает false и текст ошибки, если значения некорректны.</summary>
    public bool Apply(TaskItem t, out string error)
    {
        error = "";
        var k = Kinds[Math.Max(0, _kind.SelectedIndex)];
        int days = 0;
        for (int i = 0; i < 7; i++) if (_dayButtons[i].IsChecked == true) days |= 1 << i;
        if (k == RepeatKind.Weekdays && days == 0)
        {
            error = Loc.I["err_pick_weekday"];
            return false;
        }
        int interval = 1;
        if (k == RepeatKind.EveryNDays && (!int.TryParse(_interval.Text, out interval) || interval < 1 || interval > 365))
        {
            error = Loc.I["err_interval"];
            return false;
        }
        t.Repeat = k;
        t.RepeatInterval = interval;
        t.RepeatDays = k == RepeatKind.Weekdays ? days : 0;
        return true;
    }
}
