using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Microsoft.Win32;
using MyPlanner.Services;

namespace MyPlanner.Views;

public enum SettingsTab
{
    Appearance = 0, Language, Voice, Sound, Reminders, Droplet, Weather, Quiet, Autostart, Backup, General
}

/// <summary>Настройки: 11 разделов. Кнопка «Сохранить» применяет всё сразу, без перезапуска.</summary>
public class SettingsWindow : Window
{
    private readonly AppSettings _s = Svc.Settings;
    private readonly TabControl _tabs = new() { TabStripPlacement = Dock.Left };

    // Внешний вид / язык
    private ComboBox _theme = null!, _lang = null!;
    // Голос
    private ComboBox _speechLang = null!, _voice = null!;
    private CheckBox _tts = null!;
    private Slider _ttsRate = null!, _ttsVol = null!;
    private TextBlock _speechInfo = null!;
    // Звук
    private CheckBox _soundOn = null!;
    private ComboBox _sound = null!;
    private Slider _volume = null!;
    private TextBox _repeatMin = null!;
    // Капелька
    private CheckBox _dropOn = null!;
    private ComboBox _dropWarn = null!;
    private TextBox _dropCustom = null!, _dropText = null!;
    // Погода
    private TextBox _city = null!;
    private TextBlock _weatherInfo = null!;
    // Тихие часы
    private CheckBox _quietOn = null!;
    private TextBox _quietFrom = null!, _quietTo = null!;
    // Автозапуск / общие / резерв
    private CheckBox _auto = null!, _startMin = null!, _closeTray = null!, _minTray = null!, _backupOn = null!;
    private TextBox _backupKeep = null!;

    private static readonly int[] WarnPresets = { 60, 90, 120, 180 };

    public SettingsWindow(SettingsTab tab)
    {
        Title = Loc.I["set_title"];
        Width = 700;
        Height = 560;
        MinWidth = 600;
        MinHeight = 440;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Language = XmlLanguage.GetLanguage(Loc.I.Culture.IetfLanguageTag);
        SetResourceReference(BackgroundProperty, "AppBg");
        SetResourceReference(ForegroundProperty, "FgPrimary");

        BuildAppearance(); BuildLanguage(); BuildVoice(); BuildSound(); BuildReminders(); BuildDroplet();
        BuildWeather(); BuildQuiet(); BuildAutostart(); BuildBackup(); BuildGeneral();
        _tabs.SelectedIndex = (int)tab;

        var save = Dialogs.MakeButton(Loc.I["btn_save"], true);
        save.IsDefault = true;
        save.Click += (_, _) => { if (Apply()) Close(); };
        var cancel = Dialogs.MakeButton(Loc.I["btn_cancel"]);
        cancel.IsCancel = true;
        cancel.Click += (_, _) => Close();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);

        var root = new DockPanel { Margin = new Thickness(16) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(_tabs);
        Content = root;
    }

    // ---------- вспомогательные ----------
    private StackPanel NewTab(string headerKey)
    {
        var sp = new StackPanel { Margin = new Thickness(16, 10, 8, 10) };
        _tabs.Items.Add(new TabItem
        {
            Header = new TextBlock { Text = Loc.I[headerKey], Margin = new Thickness(6, 3, 12, 3) },
            Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        });
        return sp;
    }

    private static void Label(StackPanel p, string key, bool muted = false)
    {
        var t = new TextBlock { Text = Loc.I[key], Margin = new Thickness(0, 10, 0, 4), TextWrapping = TextWrapping.Wrap };
        if (muted) { t.FontSize = 12; t.Opacity = 0.7; }
        p.Children.Add(t);
    }

    private static CheckBox Check(StackPanel p, string key, bool value)
    {
        var c = new CheckBox { Content = Loc.I[key], IsChecked = value, Margin = new Thickness(0, 8, 0, 0) };
        p.Children.Add(c);
        return c;
    }

    private static TextBox Text(StackPanel p, string value, double width = 120)
    {
        var t = new TextBox { Text = value, Width = width, HorizontalAlignment = HorizontalAlignment.Left };
        p.Children.Add(t);
        return t;
    }

    private static Slider Sld(StackPanel p, double min, double max, double value, double tick = 1)
    {
        var s = new Slider { Minimum = min, Maximum = max, Value = value, Width = 260, HorizontalAlignment = HorizontalAlignment.Left, TickFrequency = tick, IsSnapToTickEnabled = tick < 1 ? false : true };
        p.Children.Add(s);
        return s;
    }

    // ---------- 1. Внешний вид ----------
    private void BuildAppearance()
    {
        var p = NewTab("set_appearance");
        Label(p, "set_theme");
        _theme = new ComboBox { Width = 240, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var k in new[] { "theme_system", "theme_light", "theme_dark" }) _theme.Items.Add(Loc.I[k]);
        _theme.SelectedIndex = _s.Theme switch { "light" => 1, "dark" => 2, _ => 0 };
        p.Children.Add(_theme);
    }

    // ---------- 2. Язык интерфейса ----------
    private void BuildLanguage()
    {
        var p = NewTab("set_language");
        Label(p, "set_language_ui");
        _lang = new ComboBox { Width = 280, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var l in Loc.I.Languages)
            _lang.Items.Add(new ComboBoxItem { Content = Dialogs.LanguageHeader(l), Tag = l.Code });
        foreach (ComboBoxItem it in _lang.Items)
            if ((string)it.Tag == Loc.I.Current) _lang.SelectedItem = it;
        p.Children.Add(_lang);
        Label(p, "set_language_hint", true);
    }

    // ---------- 3. Голосовой ввод ----------
    private void BuildVoice()
    {
        var p = NewTab("set_voice");
        Label(p, "set_speech_lang");
        _speechLang = new ComboBox { Width = 280, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var l in Loc.I.Languages)
            _speechLang.Items.Add(new ComboBoxItem { Content = Dialogs.LanguageHeader(l), Tag = l.Code });
        var cur = string.IsNullOrEmpty(_s.SpeechLanguage) ? Loc.I.Current : _s.SpeechLanguage;
        foreach (ComboBoxItem it in _speechLang.Items)
            if ((string)it.Tag == cur) _speechLang.SelectedItem = it;
        _speechLang.SelectionChanged += (_, _) => UpdateSpeechInfo();
        p.Children.Add(_speechLang);
        _speechInfo = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), FontSize = 12, Opacity = 0.8 };
        p.Children.Add(_speechInfo);
        UpdateSpeechInfo();

        _tts = Check(p, "set_tts_on", _s.TtsEnabled);
        Label(p, "set_tts_voice");
        _voice = new ComboBox { Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
        _voice.Items.Add(Loc.I["voice_auto"]);
        foreach (var v in Svc.Speech.Voices()) _voice.Items.Add(v);
        _voice.SelectedItem = string.IsNullOrEmpty(_s.TtsVoice) ? Loc.I["voice_auto"] : _s.TtsVoice;
        if (_voice.SelectedIndex < 0) _voice.SelectedIndex = 0;
        p.Children.Add(_voice);
        Label(p, "set_tts_rate");
        _ttsRate = Sld(p, -10, 10, _s.TtsRate);
        Label(p, "set_tts_volume");
        _ttsVol = Sld(p, 0, 100, _s.TtsVolume);
        var test = Dialogs.MakeButton(Loc.I["btn_test"]);
        test.HorizontalAlignment = HorizontalAlignment.Left;
        test.Margin = new Thickness(0, 10, 0, 0);
        test.Click += (_, _) =>
        {
            _s.TtsRate = (int)_ttsRate.Value; _s.TtsVolume = (int)_ttsVol.Value;
            _s.TtsVoice = _voice.SelectedIndex <= 0 ? "" : (string)_voice.SelectedItem;
            var code = SelectedCode(_speechLang);
            if (!Svc.Speech.Speak(Loc.I["voice_test_phrase"], code))
                Dialogs.Info(Loc.I["tts_no_voice"], this);
        };
        p.Children.Add(test);
    }

    private static string SelectedCode(ComboBox cb) => (cb.SelectedItem as ComboBoxItem)?.Tag as string ?? Loc.I.Current;

    private void UpdateSpeechInfo()
    {
        var code = SelectedCode(_speechLang);
        _speechInfo.Text = Svc.Speech.HasLocalRecognizer(code)
            ? Loc.I["speech_local"]
            : Loc.I["speech_online"];
    }

    // ---------- 4. Звуковые сигналы ----------
    private void BuildSound()
    {
        var p = NewTab("set_sound");
        _soundOn = Check(p, "set_sound_on", _s.SoundEnabled);
        Label(p, "set_sound_choice");
        _sound = new ComboBox { Width = 240, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var id in SoundService.Ids) _sound.Items.Add(Loc.I["sound_" + id]);
        _sound.SelectedIndex = Math.Max(0, Array.IndexOf(SoundService.Ids, _s.SoundId));
        p.Children.Add(_sound);
        Label(p, "set_volume");
        _volume = Sld(p, 0, 1, _s.Volume, 0.05);
        Label(p, "set_repeat_min");
        _repeatMin = Text(p, _s.RepeatMinutes.ToString(), 70);
        var test = Dialogs.MakeButton(Loc.I["btn_test_sound"]);
        test.HorizontalAlignment = HorizontalAlignment.Left;
        test.Margin = new Thickness(0, 12, 0, 0);
        test.Click += (_, _) => SoundService.Play(SoundService.Ids[Math.Max(0, _sound.SelectedIndex)], _volume.Value);
        p.Children.Add(test);
    }

    // ---------- 5. Напоминания ----------
    private void BuildReminders()
    {
        var p = NewTab("set_reminders");
        Label(p, "set_reminders_info");
        Label(p, "set_reminders_info2", true);
    }

    // ---------- 6. Капелька ----------
    private void BuildDroplet()
    {
        var p = NewTab("set_droplet");
        _dropOn = Check(p, "set_droplet_on", _s.DropletEnabled);
        Label(p, "set_droplet_before");
        _dropWarn = new ComboBox { Width = 240, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var k in new[] { "warn_1h", "warn_1h30", "warn_2h", "warn_3h", "warn_custom" }) _dropWarn.Items.Add(Loc.I[k]);
        int idx = Array.IndexOf(WarnPresets, _s.WarnMinutes);
        _dropWarn.SelectedIndex = idx >= 0 ? idx : 4;
        p.Children.Add(_dropWarn);
        Label(p, "set_droplet_custom");
        _dropCustom = Text(p, _s.WarnMinutes.ToString(), 70);
        _dropWarn.SelectionChanged += (_, _) =>
        {
            if (_dropWarn.SelectedIndex is >= 0 and < 4) _dropCustom.Text = WarnPresets[_dropWarn.SelectedIndex].ToString();
        };
        Label(p, "set_droplet_text");
        _dropText = new TextBox
        {
            Text = string.IsNullOrWhiteSpace(_s.DropletText) ? Loc.I["droplet_body"] : _s.DropletText,
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 80
        };
        p.Children.Add(_dropText);
        var reset = Dialogs.MakeButton(Loc.I["btn_reset_text"]);
        reset.HorizontalAlignment = HorizontalAlignment.Left;
        reset.Margin = new Thickness(0, 8, 0, 0);
        reset.Click += (_, _) => _dropText.Text = Loc.I["droplet_body"];
        p.Children.Add(reset);
    }

    // ---------- 7. Погода ----------
    private void BuildWeather()
    {
        var p = NewTab("set_weather");
        Label(p, "set_city");
        _city = Text(p, _s.City, 280);
        _weatherInfo = new TextBlock { Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap };
        var refresh = Dialogs.MakeButton(Loc.I["btn_refresh_weather"]);
        refresh.HorizontalAlignment = HorizontalAlignment.Left;
        refresh.Margin = new Thickness(0, 10, 0, 0);
        refresh.Click += async (_, _) =>
        {
            _s.City = _city.Text.Trim();
            _weatherInfo.Text = Loc.I["weather_loading"];
            await Svc.Weather.RefreshAsync();
            ShowWeather();
            Svc.Main?.ViewModel.RaiseWeather();
        };
        p.Children.Add(refresh);
        p.Children.Add(_weatherInfo);
        Label(p, "set_weather_hint", true);
        ShowWeather();
    }

    private void ShowWeather()
    {
        var w = Svc.Weather.Current;
        if (string.IsNullOrWhiteSpace(_s.City)) { _weatherInfo.Text = Loc.I["weather_set_city"]; return; }
        if (w == null) { _weatherInfo.Text = Loc.I["weather_no_data"]; return; }
        if (w.Code < 0) { _weatherInfo.Text = Loc.I["weather_not_found"]; return; }
        var line = $"{WeatherService.Icon(w.Code, w.IsDay)} {w.City}: {Math.Round(w.Temp):+0;-0;0}°C, {Loc.I[WeatherService.DescriptionKey(w.Code)]}\n" +
                   Loc.I.F("weather_updated", w.Updated.ToString("g", Loc.I.Culture));
        if (Svc.Weather.LastRefreshFailed) line += "\n" + Loc.I["weather_offline_note"];
        _weatherInfo.Text = line;
    }

    // ---------- 8. Тихие часы ----------
    private void BuildQuiet()
    {
        var p = NewTab("set_quiet");
        _quietOn = Check(p, "set_quiet_on", _s.QuietHoursEnabled);
        Label(p, "set_quiet_from");
        _quietFrom = Text(p, _s.QuietFrom, 80);
        Label(p, "set_quiet_to");
        _quietTo = Text(p, _s.QuietTo, 80);
        Label(p, "set_quiet_hint", true);
    }

    // ---------- 9. Автозапуск ----------
    private void BuildAutostart()
    {
        var p = NewTab("set_autostart");
        _auto = Check(p, "set_autostart_on", AutostartService.IsEnabled());
        _startMin = Check(p, "set_start_minimized", _s.StartMinimized);
    }

    // ---------- 10. Резервное копирование ----------
    private void BuildBackup()
    {
        var p = NewTab("set_backup");
        _backupOn = Check(p, "set_backup_on", _s.BackupEnabled);
        Label(p, "set_backup_keep");
        _backupKeep = Text(p, _s.BackupKeep.ToString(), 70);

        var now = Dialogs.MakeButton(Loc.I["btn_backup_now"]);
        now.HorizontalAlignment = HorizontalAlignment.Left;
        now.Margin = new Thickness(0, 12, 0, 0);
        now.Click += (_, _) =>
        {
            try { var f = Svc.Db.Backup((int)Math.Clamp(ParseInt(_backupKeep.Text, 10), 1, 100)); Dialogs.Info(Loc.I.F("backup_done", f), this); }
            catch (Exception ex) { Logger.Write("Backup failed", ex); Dialogs.Info(Loc.I["backup_failed"], this); }
        };
        p.Children.Add(now);

        var open = Dialogs.MakeButton(Loc.I["btn_open_backups"]);
        open.HorizontalAlignment = HorizontalAlignment.Left;
        open.Margin = new Thickness(0, 8, 0, 0);
        open.Click += (_, _) => { try { Process.Start(new ProcessStartInfo(Svc.Db.BackupDir) { UseShellExecute = true }); } catch { } };
        p.Children.Add(open);

        var restore = Dialogs.MakeButton(Loc.I["btn_restore_backup"]);
        restore.HorizontalAlignment = HorizontalAlignment.Left;
        restore.Margin = new Thickness(0, 8, 0, 0);
        restore.Click += (_, _) =>
        {
            var dlg = new OpenFileDialog { InitialDirectory = Svc.Db.BackupDir, Filter = "SQLite (*.db)|*.db" };
            if (dlg.ShowDialog(this) != true) return;
            if (!Dialogs.Confirm(Loc.I["msg_confirm_restore"], this)) return;
            try
            {
                Svc.Db.Backup(Math.Max(1, _s.BackupKeep)); // страховочная копия текущих данных
                Svc.Db.Restore(dlg.FileName);
                _s.Reload();
                Loc.I.SetLanguage(string.IsNullOrEmpty(_s.Language) ? Loc.Fallback : _s.Language);
                ThemeService.Apply(_s.Theme);
                Svc.RaiseChanged();
                Dialogs.Info(Loc.I["restore_done"], this);
            }
            catch (Exception ex) { Logger.Write("Restore failed", ex); Dialogs.Info(Loc.I["backup_failed"], this); }
        };
        p.Children.Add(restore);
        if (!string.IsNullOrEmpty(_s.LastBackup) && DateTime.TryParse(_s.LastBackup, CultureInfo.InvariantCulture, DateTimeStyles.None, out var lb))
            p.Children.Add(new TextBlock { Text = Loc.I.F("backup_last", lb.ToString("g", Loc.I.Culture)), Margin = new Thickness(0, 12, 0, 0), Opacity = 0.7, FontSize = 12 });
    }

    // ---------- 11. Общие ----------
    private void BuildGeneral()
    {
        var p = NewTab("set_general");
        _closeTray = Check(p, "set_close_tray", _s.CloseToTray);
        _minTray = Check(p, "set_min_tray", _s.MinimizeToTray);
        Label(p, "set_data_folder");
        var path = new TextBox { Text = Database.DataDir, IsReadOnly = true };
        p.Children.Add(path);
        Label(p, "set_version", true);
        p.Children.Add(new TextBlock { Text = "1.0.0", Opacity = 0.7, FontSize = 12 });
    }

    // ---------- Применение ----------
    private static int ParseInt(string s, int def) => int.TryParse(s?.Trim(), out var v) ? v : def;

    private static bool ValidTime(string s) => TimeSpan.TryParse(s?.Trim(), CultureInfo.InvariantCulture, out var t) && t >= TimeSpan.Zero && t < TimeSpan.FromDays(1);

    private bool Apply()
    {
        int rep = ParseInt(_repeatMin.Text, -1);
        if (rep < 1 || rep > 1440) { _tabs.SelectedIndex = (int)SettingsTab.Sound; Dialogs.Info(Loc.I["err_repeat_min"], this); return false; }
        int warn = ParseInt(_dropCustom.Text, -1);
        if (warn < 5 || warn > 1440) { _tabs.SelectedIndex = (int)SettingsTab.Droplet; Dialogs.Info(Loc.I["err_warn_min"], this); return false; }
        if (!ValidTime(_quietFrom.Text) || !ValidTime(_quietTo.Text)) { _tabs.SelectedIndex = (int)SettingsTab.Quiet; Dialogs.Info(Loc.I["err_time"], this); return false; }

        // тема
        _s.Theme = _theme.SelectedIndex switch { 1 => "light", 2 => "dark", _ => "system" };
        ThemeService.Apply(_s.Theme);

        // голос
        _s.SpeechLanguage = SelectedCode(_speechLang);
        _s.TtsEnabled = _tts.IsChecked == true;
        _s.TtsVoice = _voice.SelectedIndex <= 0 ? "" : (string)_voice.SelectedItem;
        _s.TtsRate = (int)_ttsRate.Value;
        _s.TtsVolume = (int)_ttsVol.Value;

        // звук
        _s.SoundEnabled = _soundOn.IsChecked == true;
        _s.SoundId = SoundService.Ids[Math.Max(0, _sound.SelectedIndex)];
        _s.Volume = _volume.Value;
        _s.RepeatMinutes = rep;

        // капелька
        _s.DropletEnabled = _dropOn.IsChecked == true;
        _s.WarnMinutes = warn;
        _s.DropletText = _dropText.Text.Trim() == Loc.I["droplet_body"] ? "" : _dropText.Text.Trim();

        // погода
        var cityChanged = !string.Equals(_s.City, _city.Text.Trim(), StringComparison.Ordinal);
        _s.City = _city.Text.Trim();

        // тихие часы
        _s.QuietHoursEnabled = _quietOn.IsChecked == true;
        _s.QuietFrom = _quietFrom.Text.Trim();
        _s.QuietTo = _quietTo.Text.Trim();

        // автозапуск и поведение
        _s.AutoStart = _auto.IsChecked == true;
        AutostartService.Set(_s.AutoStart);
        _s.StartMinimized = _startMin.IsChecked == true;
        _s.CloseToTray = _closeTray.IsChecked == true;
        _s.MinimizeToTray = _minTray.IsChecked == true;
        _s.BackupEnabled = _backupOn.IsChecked == true;
        _s.BackupKeep = Math.Clamp(ParseInt(_backupKeep.Text, 10), 1, 100);

        // язык интерфейса — последним, чтобы всё обновилось разом
        var code = SelectedCode(_lang);
        if (code != Loc.I.Current) Dialogs.ChangeLanguage(code);

        Svc.RaiseChanged();
        if (cityChanged) _ = Svc.Weather.RefreshAsync();
        else Svc.Main?.ViewModel.RaiseWeather();
        return true;
    }
}
