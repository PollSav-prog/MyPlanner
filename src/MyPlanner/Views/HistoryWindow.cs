using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using MyPlanner.Models;
using MyPlanner.Services;

namespace MyPlanner.Views;

/// <summary>История: выполненные, просроченные и удалённые задачи.</summary>
public class HistoryWindow : Window
{
    private readonly TabControl _tabs = new();
    private readonly ListBox _done = new(), _overdue = new(), _deleted = new();

    public HistoryWindow()
    {
        Title = Loc.I["hist_title"];
        Width = 640;
        Height = 520;
        MinWidth = 480;
        MinHeight = 360;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Language = XmlLanguage.GetLanguage(Loc.I.Culture.IetfLanguageTag);
        SetResourceReference(BackgroundProperty, "AppBg");
        SetResourceReference(ForegroundProperty, "FgPrimary");

        AddTab("hist_done", _done);
        AddTab("hist_overdue", _overdue);
        AddTab("hist_deleted", _deleted);
        foreach (var lb in new[] { _done, _overdue, _deleted })
        {
            lb.MouseDoubleClick += (_, _) => EditCurrent();
            lb.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        }

        var restore = Dialogs.MakeButton(Loc.I["btn_restore"], true);
        restore.Click += (_, _) => RestoreCurrent();
        var delForever = Dialogs.MakeButton(Loc.I["btn_delete_forever"]);
        delForever.Click += (_, _) => DeleteCurrent();
        var clear = Dialogs.MakeButton(Loc.I["btn_clear_history"]);
        clear.Click += (_, _) => ClearCurrent();
        var close = Dialogs.MakeButton(Loc.I["btn_close"]);
        close.IsCancel = true;
        close.Click += (_, _) => Close();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(restore);
        buttons.Children.Add(delForever);
        buttons.Children.Add(clear);
        buttons.Children.Add(close);

        var root = new DockPanel { Margin = new Thickness(16) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(_tabs);
        Content = root;

        Svc.DataChanged += Fill;
        Closed += (_, _) => Svc.DataChanged -= Fill;
        Fill();
    }

    private void AddTab(string key, ListBox lb)
    {
        _tabs.Items.Add(new TabItem { Header = Loc.I[key], Content = lb });
    }

    private ListBox Current => _tabs.SelectedIndex switch { 1 => _overdue, 2 => _deleted, _ => _done };
    private TaskItem? Selected => (Current.SelectedItem as ListBoxItem)?.Tag as TaskItem;

    private void Fill()
    {
        var all = Svc.Db.LoadAll();
        Fill(_done, all.Where(t => t.IsDone && !t.IsArchived).OrderByDescending(t => t.CompletedAt ?? t.Due));
        Fill(_overdue, all.Where(t => !t.IsDone && !t.IsArchived && t.Due < DateTime.Now).OrderByDescending(t => t.Due));
        Fill(_deleted, all.Where(t => t.IsArchived).OrderByDescending(t => t.Due));
    }

    private static void Fill(ListBox lb, IEnumerable<TaskItem> items)
    {
        lb.Items.Clear();
        foreach (var t in items)
        {
            var sp = new StackPanel { Margin = new Thickness(4, 4, 4, 6) };
            sp.Children.Add(new TextBlock { Text = t.Title, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
            var sub = t.DateTimeText;
            if (t.CompletedAt is DateTime c) sub += " · " + Loc.I.F("hist_completed", c.ToString("g", Loc.I.Culture));
            if (!string.IsNullOrEmpty(t.Category)) sub += " · " + t.Category;
            var line = new TextBlock { Text = sub, FontSize = 12 };
            line.SetResourceReference(TextBlock.ForegroundProperty, "FgSecondary");
            sp.Children.Add(line);
            lb.Items.Add(new ListBoxItem { Content = sp, Tag = t });
        }
    }

    private void EditCurrent()
    {
        var t = Selected;
        if (t == null) return;
        var copy = t.Clone();
        new TaskDialog(copy) { Owner = this }.ShowDialog();
    }

    private void RestoreCurrent()
    {
        var t = Selected;
        if (t == null) { Dialogs.Info(Loc.I["msg_select_task"], this); return; }
        if (_tabs.SelectedIndex == 1) { EditCurrent(); return; } // просроченную можно перенести через редактирование
        TaskOps.Restore(t);
    }

    private void DeleteCurrent()
    {
        var t = Selected;
        if (t == null) { Dialogs.Info(Loc.I["msg_select_task"], this); return; }
        if (Dialogs.Confirm(Loc.I.F("msg_confirm_delete_forever", t.Title), this)) TaskOps.DeleteForever(t);
    }

    private void ClearCurrent()
    {
        if (_tabs.SelectedIndex == 1) return; // просроченные задачи — это живые задачи, их не стираем массово
        var items = Current.Items.Cast<ListBoxItem>().Select(i => (TaskItem)i.Tag).ToList();
        if (items.Count == 0) return;
        if (!Dialogs.Confirm(Loc.I.F("msg_confirm_clear", items.Count), this)) return;
        foreach (var t in items) Svc.Db.Delete(t.Id);
        Svc.RaiseChanged();
    }
}
