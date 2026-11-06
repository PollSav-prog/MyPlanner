using System.IO;
using System.Windows;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace MyPlanner.Services;

public static class QuietMode
{
    public static bool IsActive => Svc.Settings.QuietUntil is DateTime d && d > DateTime.Now;

    public static void Set(int minutes)
    {
        Svc.Settings.QuietUntil = minutes > 0 ? DateTime.Now.AddMinutes(minutes) : null;
        Svc.RaiseChanged();
    }

    public static void Off() => Set(0);
}

/// <summary>Значок в системном трее: открыть, добавить задачу, тихий режим, выход.</summary>
public sealed class TrayService : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ContextMenuStrip _menu = new();
    public Action? OpenRequested;
    public Action? AddRequested;
    public Action? ExitRequested;

    public TrayService()
    {
        _icon = new WinForms.NotifyIcon
        {
            Icon = LoadIcon(),
            Visible = true,
            ContextMenuStrip = _menu
        };
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke();
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left) OpenRequested?.Invoke();
        };
        Rebuild();
        Loc.I.LanguageChanged += Rebuild;
        Svc.DataChanged += UpdateTooltip;
    }

    private static Drawing.Icon LoadIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/app.ico");
            var info = Application.GetResourceStream(uri);
            if (info != null) return new Drawing.Icon(info.Stream);
        }
        catch { }
        return Drawing.SystemIcons.Application;
    }

    public void Rebuild()
    {
        _menu.Items.Clear();
        _menu.Items.Add(Loc.I["tray_open"], null, (_, _) => OpenRequested?.Invoke());
        _menu.Items.Add(Loc.I["tray_add"], null, (_, _) => AddRequested?.Invoke());
        _menu.Items.Add(new WinForms.ToolStripSeparator());

        var quiet = new WinForms.ToolStripMenuItem(Loc.I["tray_quiet"]);
        quiet.DropDownItems.Add(Loc.I["quiet_30"], null, (_, _) => QuietMode.Set(30));
        quiet.DropDownItems.Add(Loc.I["quiet_60"], null, (_, _) => QuietMode.Set(60));
        quiet.DropDownItems.Add(Loc.I["quiet_120"], null, (_, _) => QuietMode.Set(120));
        quiet.DropDownItems.Add(Loc.I["quiet_off"], null, (_, _) => QuietMode.Off());
        _menu.Items.Add(quiet);

        _menu.Items.Add(new WinForms.ToolStripSeparator());
        _menu.Items.Add(Loc.I["tray_exit"], null, (_, _) => ExitRequested?.Invoke());
        UpdateTooltip();
    }

    private void UpdateTooltip()
    {
        var text = Loc.I["app_title"] + (QuietMode.IsActive ? " — " + Loc.I["quiet_on"] : "");
        _icon.Text = text.Length > 60 ? text[..60] : text;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
