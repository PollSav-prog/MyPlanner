using System.Globalization;

namespace MyPlanner.Services;

/// <summary>Настройки приложения: хранятся в таблице Settings, кешируются в памяти.</summary>
public sealed class AppSettings
{
    private readonly Database _db;
    private readonly Dictionary<string, string> _d;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public AppSettings(Database db)
    {
        _db = db;
        _d = db.LoadSettings();
    }

    public void Reload()
    {
        _d.Clear();
        foreach (var kv in _db.LoadSettings()) _d[kv.Key] = kv.Value;
    }

    private string G(string key, string def) => _d.TryGetValue(key, out var v) ? v : def;
    private int I(string key, int def) => int.TryParse(G(key, ""), NumberStyles.Integer, Inv, out var v) ? v : def;
    private double D(string key, double def) => double.TryParse(G(key, ""), NumberStyles.Float, Inv, out var v) ? v : def;
    private bool B(string key, bool def) => _d.TryGetValue(key, out var v) ? v == "1" : def;
    private void S(string key, string value) { _d[key] = value; _db.SaveSetting(key, value); }
    private void S(string key, int value) => S(key, value.ToString(Inv));
    private void S(string key, double value) => S(key, value.ToString("0.####", Inv));
    private void S(string key, bool value) => S(key, value ? "1" : "0");

    public bool Has(string key) => _d.ContainsKey(key);

    // Язык и вид
    public string Language { get => G("Language", ""); set => S("Language", value); }
    public string SpeechLanguage { get => G("SpeechLanguage", ""); set => S("SpeechLanguage", value); }
    /// <summary>system | light | dark</summary>
    public string Theme { get => G("Theme", "system"); set => S("Theme", value); }

    // Капелька и напоминания
    public bool DropletEnabled { get => B("DropletEnabled", true); set => S("DropletEnabled", value); }
    public int WarnMinutes { get => I("WarnMinutes", 120); set => S("WarnMinutes", value); }
    public string DropletText { get => G("DropletText", ""); set => S("DropletText", value); }
    public int RepeatMinutes { get => Math.Max(1, I("RepeatMinutes", 10)); set => S("RepeatMinutes", value); }

    // Звук
    public bool SoundEnabled { get => B("SoundEnabled", true); set => S("SoundEnabled", value); }
    public string SoundId { get => G("SoundId", "soft"); set => S("SoundId", value); }
    public double Volume { get => Math.Clamp(D("Volume", 0.6), 0, 1); set => S("Volume", value); }

    // Тихий режим
    public DateTime? QuietUntil
    {
        get => DateTime.TryParseExact(G("QuietUntil", ""), "yyyy-MM-dd HH:mm:ss", Inv, DateTimeStyles.None, out var d) ? d : null;
        set => S("QuietUntil", value?.ToString("yyyy-MM-dd HH:mm:ss", Inv) ?? "");
    }
    public bool QuietHoursEnabled { get => B("QuietHoursEnabled", false); set => S("QuietHoursEnabled", value); }
    public string QuietFrom { get => G("QuietFrom", "22:00"); set => S("QuietFrom", value); }
    public string QuietTo { get => G("QuietTo", "08:00"); set => S("QuietTo", value); }

    // Голос
    public bool TtsEnabled { get => B("TtsEnabled", true); set => S("TtsEnabled", value); }
    public string TtsVoice { get => G("TtsVoice", ""); set => S("TtsVoice", value); }
    public int TtsRate { get => Math.Clamp(I("TtsRate", 0), -10, 10); set => S("TtsRate", value); }
    public int TtsVolume { get => Math.Clamp(I("TtsVolume", 100), 0, 100); set => S("TtsVolume", value); }

    // Погода
    public string City { get => G("City", ""); set => S("City", value); }
    public string WeatherCache { get => G("WeatherCache", ""); set => S("WeatherCache", value); }

    // Поведение
    public bool AutoStart { get => B("AutoStart", false); set => S("AutoStart", value); }
    public bool StartMinimized { get => B("StartMinimized", false); set => S("StartMinimized", value); }
    public bool CloseToTray { get => B("CloseToTray", true); set => S("CloseToTray", value); }
    public bool MinimizeToTray { get => B("MinimizeToTray", false); set => S("MinimizeToTray", value); }
    public bool BackupEnabled { get => B("BackupEnabled", true); set => S("BackupEnabled", value); }
    public int BackupKeep { get => Math.Clamp(I("BackupKeep", 10), 1, 100); set => S("BackupKeep", value); }
    public string LastBackup { get => G("LastBackup", ""); set => S("LastBackup", value); }

    // Окно
    public double WinLeft { get => D("WinLeft", double.NaN); set => S("WinLeft", value); }
    public double WinTop { get => D("WinTop", double.NaN); set => S("WinTop", value); }
    public double WinWidth { get => D("WinWidth", 1100); set => S("WinWidth", value); }
    public double WinHeight { get => D("WinHeight", 720); set => S("WinHeight", value); }
    public bool WinMaximized { get => B("WinMaximized", false); set => S("WinMaximized", value); }

    // ---------- Производные ----------
    public bool IsQuietNow(DateTime now)
    {
        if (QuietUntil is DateTime until && until > now) return true;
        if (!QuietHoursEnabled) return false;
        if (!TimeSpan.TryParse(QuietFrom, Inv, out var from)) return false;
        if (!TimeSpan.TryParse(QuietTo, Inv, out var to)) return false;
        var t = now.TimeOfDay;
        return from <= to ? (t >= from && t < to) : (t >= from || t < to);
    }

    public int EffectiveWarnMinutes(Models.TaskItem t)
    {
        if (!DropletEnabled) return 0;
        return t.WarnMinutes >= 0 ? t.WarnMinutes : WarnMinutes;
    }
}
