using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using MyPlanner.Services;

namespace MyPlanner.Views;

/// <summary>Небольшие диалоги, построенные в коде (с переводом на выбранный язык).</summary>
public static class Dialogs
{
    public static Window Make(string title, double width = 380)
    {
        var w = new Window
        {
            Title = title,
            Width = width,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Language = XmlLanguage.GetLanguage(Loc.I.Culture.IetfLanguageTag),
        };
        w.SetResourceReference(Control.BackgroundProperty, "AppBg");
        w.SetResourceReference(Control.ForegroundProperty, "FgPrimary");
        if (Svc.Main is { IsVisible: true } m) w.Owner = m;
        return w;
    }

    public static Button MakeButton(string text, bool primary = false, double minWidth = 90)
    {
        var b = new Button { Content = text, MinWidth = minWidth, Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(6, 0, 0, 0) };
        if (primary) b.Style = (Style)Application.Current.FindResource("PrimaryButton");
        return b;
    }

    public static void Info(string message, Window? owner = null)
    {
        var w = Make(Loc.I["app_title"]);
        if (owner != null) w.Owner = owner;
        var ok = MakeButton(Loc.I["btn_ok"], true);
        ok.IsDefault = true;
        ok.Click += (_, _) => w.Close();
        w.Content = Layout(message, ok);
        w.ShowDialog();
    }

    public static bool Confirm(string message, Window? owner = null)
    {
        var w = Make(Loc.I["app_title"]);
        if (owner != null) w.Owner = owner;
        bool result = false;
        var yes = MakeButton(Loc.I["btn_yes"], true);
        var no = MakeButton(Loc.I["btn_no"]);
        yes.IsDefault = true;
        no.IsCancel = true;
        yes.Click += (_, _) => { result = true; w.Close(); };
        no.Click += (_, _) => w.Close();
        w.Content = Layout(message, no, yes);
        w.ShowDialog();
        return result;
    }

    private static UIElement Layout(string message, params Button[] buttons)
    {
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 14, Margin = new Thickness(0, 0, 0, 18) });
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var b in buttons) row.Children.Add(b);
        panel.Children.Add(row);
        return panel;
    }

    // ---------- Язык интерфейса ----------
    public static UIElement LanguageHeader(LangInfo l)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        var badge = new Border
        {
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 0, 10, 0),
            MinWidth = 32,
            Child = new TextBlock { Text = l.Badge, FontWeight = FontWeights.SemiBold, FontSize = 12, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center }
        };
        badge.SetResourceReference(Border.BackgroundProperty, "PrimaryBrush");
        sp.Children.Add(badge);
        sp.Children.Add(new TextBlock { Text = l.Native, FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
        return sp;
    }

    /// <summary>Меню выбора языка у иконки на правой панели. Язык меняется без перезапуска.</summary>
    public static void ShowLanguageMenu(FrameworkElement target)
    {
        var menu = new ContextMenu { PlacementTarget = target, Placement = System.Windows.Controls.Primitives.PlacementMode.Left };
        foreach (var l in Loc.I.Languages)
        {
            var item = new MenuItem { Header = LanguageHeader(l), IsCheckable = false, IsChecked = l.Code == Loc.I.Current };
            var code = l.Code;
            item.Click += (_, _) => ChangeLanguage(code);
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    public static void ChangeLanguage(string code)
    {
        Svc.Settings.Language = code;
        Loc.I.SetLanguage(code);
        Svc.RaiseChanged();
    }

    /// <summary>Выбор языка при первом запуске.</summary>
    public static string PickLanguageFirstRun(string preselect)
    {
        var w = Make("Мой планировщик / My Planner", 360);
        w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        w.ShowInTaskbar = true;
        string chosen = preselect;
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = "Выберите язык / Choose your language", FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12), TextWrapping = TextWrapping.Wrap });
        var list = new ListBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent };
        foreach (var l in Loc.I.Languages)
            list.Items.Add(new ListBoxItem { Content = LanguageHeader(l), Tag = l.Code, Padding = new Thickness(8, 6, 8, 6) });
        foreach (ListBoxItem it in list.Items)
            if ((string)it.Tag == preselect) list.SelectedItem = it;
        if (list.SelectedItem == null && list.Items.Count > 0) list.SelectedIndex = 0;
        list.SelectionChanged += (_, _) => { if (list.SelectedItem is ListBoxItem i) chosen = (string)i.Tag; };
        panel.Children.Add(list);
        var ok = MakeButton("OK", true);
        ok.IsDefault = true;
        ok.HorizontalAlignment = HorizontalAlignment.Right;
        ok.Margin = new Thickness(0, 14, 0, 0);
        ok.Click += (_, _) => w.Close();
        panel.Children.Add(ok);
        w.Content = panel;
        w.ShowDialog();
        return chosen;
    }
}

/// <summary>Окно выбора периода тихого режима.</summary>
public class QuietDialog : Window
{
    public QuietDialog()
    {
        var w = this;
        Title = Loc.I["quiet_title"];
        Width = 360;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Language = XmlLanguage.GetLanguage(Loc.I.Culture.IetfLanguageTag);
        SetResourceReference(BackgroundProperty, "AppBg");
        SetResourceReference(ForegroundProperty, "FgPrimary");

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = Loc.I["quiet_hint"], TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });

        void Add(string text, int minutes, bool primary = false)
        {
            var b = Dialogs.MakeButton(text, primary, 0);
            b.Margin = new Thickness(0, 0, 0, 8);
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            b.Click += (_, _) => { QuietMode.Set(minutes); w.Close(); };
            panel.Children.Add(b);
        }

        Add(Loc.I["quiet_30"], 30);
        Add(Loc.I["quiet_60"], 60);
        Add(Loc.I["quiet_120"], 120);
        Add(Loc.I["quiet_240"], 240);
        var tomorrow = DateTime.Today.AddDays(1).AddHours(8);
        Add(Loc.I["quiet_morning"], (int)Math.Ceiling((tomorrow - DateTime.Now).TotalMinutes));
        if (QuietMode.IsActive) Add(Loc.I["quiet_off"], 0, true);
        var close = Dialogs.MakeButton(Loc.I["btn_close"]);
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.IsCancel = true;
        close.Click += (_, _) => w.Close();
        panel.Children.Add(close);
        Content = panel;
    }
}
