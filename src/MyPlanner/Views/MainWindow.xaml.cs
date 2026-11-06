using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using MyPlanner.Models;
using MyPlanner.Services;
using MyPlanner.ViewModels;

namespace MyPlanner.Views;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }
    public bool ExitRequested { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainViewModel();
        DataContext = ViewModel;
        Language = XmlLanguage.GetLanguage(Loc.I.Culture.IetfLanguageTag);
        Loc.I.LanguageChanged += () => Language = XmlLanguage.GetLanguage(Loc.I.Culture.IetfLanguageTag);
        RestoreGeometry();
        ViewModel.RaiseWeather();
    }

    // ---------- Положение и размер окна ----------
    private void RestoreGeometry()
    {
        var s = Svc.Settings;
        Width = Math.Max(MinWidth, s.WinWidth);
        Height = Math.Max(MinHeight, s.WinHeight);
        if (!double.IsNaN(s.WinLeft) && !double.IsNaN(s.WinTop))
        {
            // не выходим за пределы экрана
            double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
            double vr = vl + SystemParameters.VirtualScreenWidth, vb = vt + SystemParameters.VirtualScreenHeight;
            if (s.WinLeft >= vl - 20 && s.WinTop >= vt - 20 && s.WinLeft < vr - 100 && s.WinTop < vb - 100)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = s.WinLeft;
                Top = s.WinTop;
            }
            else WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        else WindowStartupLocation = WindowStartupLocation.CenterScreen;
        if (s.WinMaximized) WindowState = WindowState.Maximized;
    }

    private void SaveGeometry()
    {
        var s = Svc.Settings;
        var b = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (b.IsEmpty || double.IsNaN(b.Left)) return;
        s.WinLeft = b.Left;
        s.WinTop = b.Top;
        s.WinWidth = b.Width;
        s.WinHeight = b.Height;
        s.WinMaximized = WindowState == WindowState.Maximized;
    }

    public void ShowFromTray()
    {
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Topmost = true; Topmost = false; // выводим поверх остальных окон
        Focus();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        SaveGeometry();
        if (!ExitRequested && Svc.Settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && Svc.Settings.MinimizeToTray)
            Hide();
    }

    // ---------- Календарь ----------
    private void Cell_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is DayCell cell)
            ViewModel.SelectDate(cell.Date);
    }

    private void Droplet_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement fe && fe.DataContext is DayCell cell)
        {
            ViewModel.SelectDate(cell.Date);
            var tasks = ViewModel.DropletTasksOn(cell.Date);
            if (tasks.Count > 0) NotificationCoordinator.ShowWarningCard(tasks);
        }
    }

    // ---------- Список ----------
    private void TaskList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.SelectedTask != null && e.OriginalSource is FrameworkElement fe && fe.DataContext is TaskItem)
            ViewModel.EditTask(ViewModel.SelectedTask);
    }

    private void LangButton_Click(object sender, RoutedEventArgs e) => Dialogs.ShowLanguageMenu(LangButton);
}
