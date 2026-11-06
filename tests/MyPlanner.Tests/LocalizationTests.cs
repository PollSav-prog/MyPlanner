using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace MyPlanner.Tests;

/// <summary>Проверка полноты переводов: ни одна кнопка не должна остаться без текста на выбранном языке.</summary>
public class LocalizationTests
{
    private static readonly string[] Codes = { "ru-RU", "en-US", "de-DE", "fr-FR" };

    private static string SrcDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src", "MyPlanner", "Locales")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "MyPlanner");
    }

    private static Dictionary<string, string> Load(string code)
    {
        var json = File.ReadAllText(Path.Combine(SrcDir(), "Locales", code + ".json"));
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
    }

    [Fact]
    public void AllLanguages_HaveSameKeys()
    {
        var ru = Load("ru-RU");
        foreach (var code in Codes.Skip(1))
        {
            var t = Load(code);
            Assert.Empty(ru.Keys.Except(t.Keys));
            Assert.Empty(t.Keys.Except(ru.Keys));
        }
    }

    [Fact]
    public void NoEmptyTranslations()
    {
        foreach (var code in Codes)
            foreach (var kv in Load(code))
                Assert.False(string.IsNullOrWhiteSpace(kv.Value), $"{code}: пустой перевод «{kv.Key}»");
    }

    [Fact]
    public void Placeholders_MatchAcrossLanguages()
    {
        var ru = Load("ru-RU");
        foreach (var code in Codes.Skip(1))
        {
            var t = Load(code);
            foreach (var kv in ru)
            {
                var a = Regex.Matches(kv.Value, @"\{\d+\}").Select(m => m.Value).OrderBy(x => x);
                var b = Regex.Matches(t[kv.Key], @"\{\d+\}").Select(m => m.Value).OrderBy(x => x);
                Assert.True(a.SequenceEqual(b), $"{code}: не совпадают подстановки в «{kv.Key}»");
            }
        }
    }

    [Fact]
    public void EveryKeyUsedInSource_ExistsInRussian()
    {
        var ru = Load("ru-RU");
        var used = new HashSet<string>();
        foreach (var file in Directory.GetFiles(SrcDir(), "*.*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".cs") || f.EndsWith(".xaml")) && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)))
        {
            var s = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(s, @"Loc\.I\[""([a-z0-9_]+)""\]")) used.Add(m.Groups[1].Value);
            foreach (Match m in Regex.Matches(s, @"Loc\.I\.(?:F|In)\((?:code, )?""([a-z0-9_]+)""")) used.Add(m.Groups[1].Value);
            foreach (Match m in Regex.Matches(s, @"\{svc:T ([a-z0-9_]+)\}")) used.Add(m.Groups[1].Value);
        }
        Assert.NotEmpty(used);
        Assert.Empty(used.Where(k => !ru.ContainsKey(k)));
    }

    [Fact]
    public void LanguageFiles_HaveNameAndBadge()
    {
        foreach (var code in Codes)
        {
            var t = Load(code);
            Assert.True(t.ContainsKey("_native"));
            Assert.True(t.ContainsKey("_badge"));
        }
    }
}
