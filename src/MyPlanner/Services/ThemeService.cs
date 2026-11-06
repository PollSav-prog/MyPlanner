using System.Windows;
using Microsoft.Win32;

namespace MyPlanner.Services;

/// <summary>Светлая, тёмная и системная темы. Переключаются без перезапуска.</summary>
public static class ThemeService
{
    private static string _mode = "system";

    public static void Init()
    {
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (_mode == "system" && e.Category == UserPreferenceCategory.General)
                Application.Current.Dispatcher.InvokeAsync(() => Apply(_mode));
        };
    }

    public static bool SystemIsDark()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    public static void Apply(string mode)
    {
        _mode = mode;
        bool dark = mode == "dark" || (mode == "system" && SystemIsDark());
        var app = Application.Current;

        // 1) наша палитра
        var dict = new ResourceDictionary
        {
            Source = new Uri(dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative)
        };
        // ищем нашу палитру по ключу, а не по номеру: Fluent-тема WPF сама добавляет словари ресурсов
        var merged = app.Resources.MergedDictionaries;
        int idx = -1;
        for (int i = 0; i < merged.Count; i++)
            if (merged[i].Contains("AppBg")) { idx = i; break; }
        if (idx >= 0) merged[idx] = dict; else merged.Insert(0, dict);

        // 2) стандартные элементы управления (Fluent-тема WPF)
        try
        {
            app.ThemeMode = dark ? ThemeMode.Dark : ThemeMode.Light;
        }
        catch (Exception ex)
        {
            Logger.Write("ThemeMode failed", ex);
        }
    }
}
