using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace MyPlanner.Services;

public record LangInfo(string Code, string Native, string Badge);

/// <summary>
/// Локализация. Тексты лежат в файлах Locales/*.json (рядом с программой и в %APPDATA%\MyPlanner\Locales).
/// Чтобы добавить язык, достаточно положить новый файл, например es-ES.json, со служебными полями
/// "_native" и "_badge". Отсутствующие ключи берутся из русского файла.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc I { get; } = new();
    public const string Fallback = "ru-RU";

    private readonly Dictionary<string, Dictionary<string, string>> _tables = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _cur = new();
    private Dictionary<string, string> _fb = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? LanguageChanged;

    public string Current { get; private set; } = Fallback;
    public CultureInfo Culture { get; private set; } = new CultureInfo("ru-RU");
    public List<LangInfo> Languages { get; } = new();

    public void Load()
    {
        _tables.Clear();
        Languages.Clear();
        var dirs = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "Locales"),
            Path.Combine(Database.DataDir, "Locales")
        };
        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    var code = Path.GetFileNameWithoutExtension(file);
                    var dict = ParseFile(file);
                    _tables[code] = dict;
                }
                catch (Exception ex)
                {
                    Logger.Write("Locale load failed: " + file, ex);
                }
            }
        }
        foreach (var kv in _tables.OrderBy(k => OrderOf(k.Key)))
        {
            kv.Value.TryGetValue("_native", out var native);
            kv.Value.TryGetValue("_badge", out var badge);
            Languages.Add(new LangInfo(kv.Key, native ?? kv.Key, badge ?? kv.Key[..2].ToUpperInvariant()));
        }
        _tables.TryGetValue(Fallback, out var fb);
        _fb = fb ?? new();
    }

    private static int OrderOf(string code) => code switch
    {
        "ru-RU" => 0, "en-US" => 1, "de-DE" => 2, "fr-FR" => 3, _ => 10
    };

    private static Dictionary<string, string> ParseFile(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var d = new Dictionary<string, string>();
        foreach (var p in doc.RootElement.EnumerateObject())
            d[p.Name] = p.Value.GetString() ?? "";
        return d;
    }

    public bool IsSupported(string code) => _tables.ContainsKey(code);

    /// <summary>Подбирает поддерживаемый язык по языку Windows или null.</summary>
    public string? DetectWindowsLanguage()
    {
        var two = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;
        return Languages.FirstOrDefault(l => l.Code.StartsWith(two + "-", StringComparison.OrdinalIgnoreCase))?.Code;
    }

    public void SetLanguage(string code)
    {
        if (!_tables.ContainsKey(code)) code = Fallback;
        Current = code;
        _cur = _tables[code];
        try { Culture = new CultureInfo(code); } catch { Culture = new CultureInfo("en-US"); }
        CultureInfo.DefaultThreadCurrentCulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
        Thread.CurrentThread.CurrentCulture = Culture;
        Thread.CurrentThread.CurrentUICulture = Culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        LanguageChanged?.Invoke();
    }

    public string this[string key]
    {
        get
        {
            if (_cur.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v)) return v;
            if (_fb.TryGetValue(key, out var f) && !string.IsNullOrEmpty(f)) return f;
            return key;
        }
    }

    public string F(string key, params object[] args)
    {
        try { return string.Format(Culture, this[key], args); }
        catch (FormatException) { return this[key]; }
    }

    /// <summary>Перевод в указанном языке (например, для озвучивания на языке речи, а не интерфейса).</summary>
    public string In(string code, string key, params object[] args)
    {
        string fmt;
        if (_tables.TryGetValue(code, out var t) && t.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v)) fmt = v;
        else fmt = this[key];
        try { return string.Format(new CultureInfo(code), fmt, args); }
        catch (FormatException) { return fmt; }
    }

    /// <summary>Ключи, которых нет в указанном языке, но есть в русском (для проверки полноты переводов).</summary>
    public IEnumerable<string> FindMissing(string code)
    {
        if (!_tables.TryGetValue(code, out var t)) return _fb.Keys;
        return _fb.Keys.Where(k => !t.ContainsKey(k) || string.IsNullOrWhiteSpace(t[k]));
    }

    public IReadOnlyDictionary<string, string>? Table(string code) => _tables.TryGetValue(code, out var t) ? t : null;
}

/// <summary>Разметка XAML: {svc:T ключ} — привязка к переводу, обновляется при смене языка.</summary>
public class TExtension : MarkupExtension
{
    public string Key { get; set; } = "";
    public TExtension() { }
    public TExtension(string key) { Key = key; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var b = new Binding($"[{Key}]") { Source = Loc.I, Mode = BindingMode.OneWay };
        return b.ProvideValue(serviceProvider);
    }
}
