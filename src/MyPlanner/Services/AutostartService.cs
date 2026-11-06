using Microsoft.Win32;

namespace MyPlanner.Services;

public static class AutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MyPlanner";

    public static bool IsEnabled()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue(ValueName) != null;
        }
        catch { return false; }
    }

    public static void Set(bool enabled)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled)
            {
                var exe = Environment.ProcessPath ?? "";
                k.SetValue(ValueName, $"\"{exe}\" --minimized");
            }
            else
            {
                k.DeleteValue(ValueName, false);
            }
        }
        catch (Exception ex)
        {
            Logger.Write("Autostart failed", ex);
        }
    }
}
