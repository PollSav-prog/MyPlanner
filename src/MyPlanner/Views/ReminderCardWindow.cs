using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Shapes = System.Windows.Shapes;
using MyPlanner.Models;
using MyPlanner.Services;

namespace MyPlanner.Views;

public enum ReminderCardMode { Warning, Due }

/// <summary>
/// Карточка напоминания. Warning — «капелька» (предупреждение за 1,5–2 часа),
/// Due — основное напоминание в назначенное время.
/// </summary>
public class ReminderCardWindow : Window
{
    private readonly ReminderCardMode _mode;
    private readonly List<TaskItem> _tasks;

    public ReminderCardWindow(ReminderCardMode mode, List<TaskItem> tasks)
    {
        _mode = mode;
        _tasks = tasks;
        Title = Loc.I[mode == ReminderCardMode.Warning ? "droplet_window" : "due_window"];
        Width = 420;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Language = XmlLanguage.GetLanguage(Loc.I.Culture.IetfLanguageTag);
        SetResourceReference(BackgroundProperty, "AppBg");
        SetResourceReference(ForegroundProperty, "FgPrimary");

        Content = Build();
        Loaded += (_, _) =>
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Right - ActualWidth - 16;
            Top = wa.Bottom - ActualHeight - 16;
        };
    }

    private UIElement Build()
    {
        var first = _tasks[0];
        var root = new StackPanel { Margin = new Thickness(20) };

        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        head.Children.Add(MakeIcon());
        var headText = new TextBlock
        {
            Text = _mode == ReminderCardMode.Warning ? Loc.I["droplet_title"] : Loc.I["due_title"],
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            MaxWidth = 320
        };
        headText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        head.Children.Add(headText);
        root.Children.Add(head);

        if (_mode == ReminderCardMode.Warning)
        {
            var body = string.IsNullOrWhiteSpace(Svc.Settings.DropletText) ? Loc.I["droplet_body"] : Svc.Settings.DropletText;
            root.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
        }

        root.Children.Add(new TextBlock
        {
            Text = Loc.I.F("droplet_time", first.Due.ToString("t", Loc.I.Culture)),
            FontWeight = FontWeights.SemiBold
        });
        root.Children.Add(new TextBlock
        {
            Text = Loc.I.F("droplet_name", first.Title),
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0)
        });

        if (_tasks.Count > 1)
        {
            var more = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            more.Children.Add(new TextBlock { Text = Loc.I["more_tasks"], Opacity = 0.7, FontSize = 12 });
            foreach (var t in _tasks.Skip(1).Take(6))
                more.Children.Add(new TextBlock { Text = $"{t.Due.ToString("t", Loc.I.Culture)} — {t.Title}", TextTrimming = TextTrimming.CharacterEllipsis });
            root.Children.Add(more);
        }

        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        if (_mode == ReminderCardMode.Warning)
        {
            buttons.Children.Add(Btn(Loc.I["btn_view_task"], true, () => { View(first); Close(); }));
            buttons.Children.Add(Btn(Loc.I["btn_postpone"], false, () => { NotificationCoordinator.PostponeWarnings(15); Close(); }));
            buttons.Children.Add(Btn(Loc.I["btn_close"], false, Close));
        }
        else
        {
            buttons.Children.Add(Btn(Loc.I["btn_done"], true, () => { TaskOps.Complete(first); SafeClose(); }));
            buttons.Children.Add(Btn(Loc.I["btn_snooze_10"], false, () => { TaskOps.Snooze(first, 10); SafeClose(); }));
            buttons.Children.Add(Btn(Loc.I["btn_snooze_30"], false, () => { TaskOps.Snooze(first, 30); SafeClose(); }));
            buttons.Children.Add(Btn(Loc.I["btn_snooze_60"], false, () => { TaskOps.Snooze(first, 60); SafeClose(); }));
            buttons.Children.Add(Btn(Loc.I["btn_view_task"], false, () => { View(first); }));
        }
        root.Children.Add(buttons);
        return root;
    }

    private void SafeClose()
    {
        if (IsLoaded) Close();
    }

    private static void View(TaskItem t)
    {
        Svc.Main?.ShowFromTray();
        Svc.Main?.ViewModel.SelectDate(t.Due.Date, t);
    }

    private static Button Btn(string text, bool primary, Action click)
    {
        var b = Dialogs.MakeButton(text, primary, 0);
        b.Margin = new Thickness(6, 4, 0, 0);
        b.Click += (_, _) => click();
        return b;
    }

    private UIElement MakeIcon()
    {
        if (_mode == ReminderCardMode.Due)
        {
            var t = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 30 };
            t.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            return t;
        }
        var drop = new Shapes.Path
        {
            Data = (Geometry)Application.Current.FindResource("DropletGeometry"),
            Width = 26,
            Height = 36,
            Stretch = Stretch.Uniform,
            Stroke = new SolidColorBrush(Color.FromRgb(0xA8, 0x32, 0x2F)),
            StrokeThickness = 0.8,
            Effect = new DropShadowEffect { Color = Color.FromRgb(0xE5, 0x39, 0x35), BlurRadius = 10, ShadowDepth = 0, Opacity = 0.6 },
            Fill = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.35, 0.65),
                Center = new Point(0.5, 0.62),
                RadiusX = 0.75,
                RadiusY = 0.75,
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(0xFF, 0x9A, 0x9A), 0),
                    new GradientStop(Color.FromRgb(0xE5, 0x39, 0x35), 0.55),
                    new GradientStop(Color.FromRgb(0x9E, 0x1F, 0x1C), 1)
                }
            }
        };
        return drop;
    }
}
