using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using MyPlanner.Models;
using MyPlanner.Services;

namespace MyPlanner.Views;

/// <summary>Создание и редактирование задачи.</summary>
public class TaskDialog : Window
{
    private readonly TaskItem _task;
    private readonly TextBox _title = new();
    private readonly TextBox _desc = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 70, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly DatePicker _date = new();
    private readonly TextBox _time = new() { Width = 80 };
    private readonly ComboBox _prio = new();
    private readonly ComboBox _cat = new() { IsEditable = true };
    private readonly CheckBox _remind = new();
    private readonly ComboBox _warn = new();
    private readonly RepeatPanel _repeat = new();

    private static readonly (int minutes, string key)[] WarnOptions =
    {
        (-1, "warn_default"), (0, "warn_off"), (30, "warn_30"), (60, "warn_1h"),
        (90, "warn_1h30"), (120, "warn_2h"), (180, "warn_3h")
    };

    public TaskDialog(TaskItem task)
    {
        _task = task;
        Title = Loc.I[task.Id == 0 ? "dlg_new_task" : "dlg_edit_task"];
        Width = 480;
        SizeToContent = SizeToContent.Height;
        MaxHeight = SystemParameters.WorkArea.Height - 40;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Language = XmlLanguage.GetLanguage(Loc.I.Culture.IetfLanguageTag);
        SetResourceReference(BackgroundProperty, "AppBg");
        SetResourceReference(ForegroundProperty, "FgPrimary");

        var root = new StackPanel { Margin = new Thickness(20) };
        Add(root, "lbl_title", _title);
        Add(root, "lbl_description", _desc);

        var dt = new StackPanel { Orientation = Orientation.Horizontal };
        _date.Width = 170;
        _date.Margin = new Thickness(0, 0, 14, 0);
        dt.Children.Add(_date);
        dt.Children.Add(_time);
        dt.Children.Add(new TextBlock { Text = "  (HH:mm)", VerticalAlignment = VerticalAlignment.Center, Opacity = 0.6 });
        AddRow(root, Loc.I["lbl_date_time"], dt);

        foreach (var k in new[] { "prio_low", "prio_normal", "prio_high" }) _prio.Items.Add(new ComboBoxItem { Content = Loc.I[k] });
        Add(root, "lbl_priority", _prio);

        foreach (var k in new[] { "cat_personal", "cat_work", "cat_health", "cat_home", "cat_study" }) _cat.Items.Add(Loc.I[k]);
        Add(root, "lbl_category", _cat);

        _remind.Content = Loc.I["lbl_remind"];
        _remind.Margin = new Thickness(0, 6, 0, 6);
        root.Children.Add(_remind);

        foreach (var (_, key) in WarnOptions) _warn.Items.Add(new ComboBoxItem { Content = Loc.I[key] });
        Add(root, "lbl_warn", _warn);

        AddRow(root, Loc.I["lbl_repeat"], _repeat);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = Dialogs.MakeButton(Loc.I["btn_cancel"]);
        cancel.IsCancel = true;
        var save = Dialogs.MakeButton(Loc.I["btn_save"], true);
        save.IsDefault = true;
        save.Click += (_, _) => OnSave();
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        root.Children.Add(buttons);

        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Load();
        Loaded += (_, _) => { _title.Focus(); _title.SelectAll(); };
    }

    private static void Add(StackPanel root, string key, Control c) => AddRow(root, Loc.I[key], c);

    private static void AddRow(StackPanel root, string label, UIElement c)
    {
        root.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 3), Opacity = 0.75, FontSize = 12 });
        root.Children.Add(c);
    }

    private void Load()
    {
        _title.Text = _task.Title;
        _desc.Text = _task.Description;
        _date.SelectedDate = _task.Due.Date;
        _time.Text = _task.Due.ToString("HH:mm");
        _prio.SelectedIndex = Math.Clamp(_task.Priority, 0, 2);
        _cat.Text = _task.Category;
        _remind.IsChecked = _task.RemindEnabled;
        int wi = Array.FindIndex(WarnOptions, o => o.minutes == _task.WarnMinutes);
        _warn.SelectedIndex = wi >= 0 ? wi : 0;
        _repeat.Load(_task);
    }

    public static bool TryParseTime(string s, out TimeSpan t)
    {
        t = default;
        var m = Regex.Match((s ?? "").Trim(), @"^(\d{1,2})(?:[:.](\d{2}))?$");
        if (!m.Success) return false;
        int h = int.Parse(m.Groups[1].Value);
        int mi = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
        if (h > 23 || mi > 59) return false;
        t = new TimeSpan(h, mi, 0);
        return true;
    }

    private void OnSave()
    {
        if (string.IsNullOrWhiteSpace(_title.Text)) { Dialogs.Info(Loc.I["err_title_empty"], this); _title.Focus(); return; }
        if (_date.SelectedDate == null) { Dialogs.Info(Loc.I["err_date"], this); return; }
        if (!TryParseTime(_time.Text, out var time)) { Dialogs.Info(Loc.I["err_time"], this); _time.Focus(); return; }
        if (!_repeat.Apply(_task, out var err)) { Dialogs.Info(err, this); return; }

        _task.Title = _title.Text.Trim();
        _task.Description = _desc.Text.Trim();
        _task.Due = _date.SelectedDate.Value.Date + time;
        _task.Priority = Math.Max(0, _prio.SelectedIndex);
        _task.Category = (_cat.Text ?? "").Trim();
        _task.RemindEnabled = _remind.IsChecked == true;
        _task.WarnMinutes = WarnOptions[Math.Max(0, _warn.SelectedIndex)].minutes;
        TaskOps.Save(_task);
        DialogResult = true;
    }
}
