using System.Text.RegularExpressions;
using MyPlanner.Models;

namespace MyPlanner.Services;

public class ParsedTask
{
    public string Title { get; set; } = "";
    public DateTime? Date { get; set; }
    public TimeSpan? Time { get; set; }
    /// <summary>Час назван без «утра/вечера» — нужно подтверждение (например, «в три часа»).</summary>
    public bool AmbiguousHour { get; set; }
    public RepeatKind Repeat { get; set; } = RepeatKind.None;
    public int RepeatInterval { get; set; } = 1;
    public int RepeatDays { get; set; }
    public bool HasTime => Time.HasValue;

    public DateTime Due => (Date ?? DateTime.Today).Date + (Time ?? new TimeSpan(9, 0, 0));
}

/// <summary>
/// Разбор фраз вида «Завтра в три часа позвонить врачу» на русском, английском, немецком и французском.
/// Находит дату, время, повторение и название задачи.
/// </summary>
public static class NaturalDateParser
{
    private class Rules
    {
        public string[] Numbers = Array.Empty<string>();           // слова-числа (по возрастанию значения 0..24)
        public string Today = "", Tomorrow = "", DayAfter = "";
        public string[] Weekdays = new string[7];                  // Пн..Вс
        public string[] Months = new string[12];
        public string RepDaily = "", RepWeekly = "", RepMonthly = "", RepYearly = "", RepEveryN = "";
        public string RepWeekdayPrefix = "";                       // «каждый», «every», «jeden»…
        public string WeekdayPrefix = "";                          // «в», «on», «am»…
        public string Offset = "";                                 // «через …»
        public string[] TimePatterns = Array.Empty<string>();
        public string[] DatePatterns = Array.Empty<string>();
        public string Am = "", Pm = "", Evening = "", Night = "";
        public string LeadFiller = "";
        public string EdgePreps = "";
    }

    // слова-числа: индекс = значение
    private static readonly string[] NumRu = { "ноль", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять", "десять", "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать", "семнадцать", "восемнадцать", "девятнадцать", "двадцать", "двадцать один", "двадцать два", "двадцать три", "двадцать четыре" };
    private static readonly string[] NumEn = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty", "twenty one", "twenty two", "twenty three", "twenty four" };
    private static readonly string[] NumDe = { "null", "eins", "zwei", "drei", "vier", "fünf", "sechs", "sieben", "acht", "neun", "zehn", "elf", "zwölf", "dreizehn", "vierzehn", "fünfzehn", "sechzehn", "siebzehn", "achtzehn", "neunzehn", "zwanzig", "einundzwanzig", "zweiundzwanzig", "dreiundzwanzig", "vierundzwanzig" };
    private static readonly string[] NumFr = { "zéro", "un", "deux", "trois", "quatre", "cinq", "six", "sept", "huit", "neuf", "dix", "onze", "douze", "treize", "quatorze", "quinze", "seize", "dix-sept", "dix-huit", "dix-neuf", "vingt", "vingt et un", "vingt-deux", "vingt-trois", "vingt-quatre" };

    private static string NumAlt(string[] words, params (string word, int val)[] extra)
    {
        var all = words.Concat(extra.Select(e => e.word)).Distinct()
            .OrderByDescending(w => w.Length).Select(Regex.Escape);
        return string.Join("|", all);
    }

    private static int NumValue(string[] words, string token, params (string word, int val)[] extra)
    {
        token = Regex.Replace(token.Trim().ToLowerInvariant(), @"\s+", " ");
        if (int.TryParse(token, out var n)) return n;
        for (int i = 0; i < words.Length; i++) if (words[i] == token) return i;
        foreach (var e in extra) if (e.word == token) return e.val;
        return -1;
    }

    private static readonly (string, int)[] ExtraRu = { ("одну", 1), ("одного", 1), ("две", 2), ("двух", 2), ("трёх", 3), ("трех", 3), ("пол", 0) };
    private static readonly (string, int)[] ExtraEn = { ("a", 1), ("an", 1) };
    private static readonly (string, int)[] ExtraDe = { ("ein", 1), ("einer", 1), ("einem", 1), ("eine", 1) };
    private static readonly (string, int)[] ExtraFr = { ("une", 1), ("vingt-et-un", 21) };

    private static readonly Dictionary<string, Rules> All = Build();

    private static Dictionary<string, Rules> Build()
    {
        var ru = new Rules();
        var nru = NumAlt(NumRu, ExtraRu);
        ru.Numbers = NumRu;
        ru.Today = @"\bсегодня\b"; ru.Tomorrow = @"\bзавтра\b"; ru.DayAfter = @"\bпослезавтра\b";
        ru.Weekdays = new[] { @"понедельник\w*", @"вторник\w*", @"сред[ауые]\b", @"четверг\w*", @"пятниц\w*", @"суббот\w*", @"воскресень\w*" };
        ru.Months = new[] { @"январ\w*", @"феврал\w*", @"март\w*", @"апрел\w*", @"ма[яй]\b", @"июн\w*", @"июл\w*", @"август\w*", @"сентябр\w*", @"октябр\w*", @"ноябр\w*", @"декабр\w*" };
        ru.RepDaily = @"\b(?:кажд(?:ый|ую|ое)\s+день|ежедневно)\b";
        ru.RepWeekly = @"\b(?:кажд(?:ую|ая)\s+неделю|еженедельно)\b";
        ru.RepMonthly = @"\b(?:кажд(?:ый|ое)\s+месяц|ежемесячно)\b";
        ru.RepYearly = @"\b(?:кажд(?:ый|ое)\s+год|ежегодно)\b";
        ru.RepEveryN = $@"\bкажд(?:ые|ых)\s+(?<n>\d+|{nru})\s+(?:дня|дней|день)\b";
        ru.RepWeekdayPrefix = @"\bкажд(?:ый|ую|ое|ые)\s+";
        ru.WeekdayPrefix = @"(?:\b(?:в|во|на)\s+)?";
        ru.Offset = $@"\bчерез\s+(?:(?<n>\d+|{nru})\s+)?(?<u>минут\w*|час\w*|д(?:ень|ня|ней)\b|недел\w*)";
        ru.TimePatterns = new[]
        {
            @"(?:\b(?:в|к)\s+)?(?<!\d)(?<h>\d{1,2})[:.](?<m>\d{2})(?!\d)(?:\s*(?<p>утра|вечера|дня|ночи))?",
            $@"\b(?:в|к)\s+(?<h>\d{{1,2}}|{nru})(?:\s*(?:часов|часа|час|ч)\b)?(?:\s*(?<p>утра|вечера|дня|ночи)\b)?"
        };
        var mru = string.Join("|", ru.Months);
        ru.DatePatterns = new[]
        {
            $@"(?<d>\d{{1,2}})(?:-?го)?\s+(?<mon>{mru})"
        };
        ru.Am = "утра"; ru.Pm = "дня"; ru.Evening = "вечера"; ru.Night = "ночи";
        ru.LeadFiller = @"^(?:напомни(?:ть)?(?:\s+мне)?|нужно|надо|пожалуйста)\s+";
        ru.EdgePreps = "в|во|на|к|по|и";

        var en = new Rules();
        var nen = NumAlt(NumEn, ExtraEn);
        en.Numbers = NumEn;
        en.Today = @"\btoday\b"; en.Tomorrow = @"\btomorrow\b"; en.DayAfter = @"\bthe day after tomorrow\b|\bday after tomorrow\b";
        en.Weekdays = new[] { @"monday", @"tuesday", @"wednesday", @"thursday", @"friday", @"saturday", @"sunday" };
        en.Months = new[] { @"january|jan\b", @"february|feb\b", @"march|mar\b", @"april|apr\b", @"may\b", @"june|jun\b", @"july|jul\b", @"august|aug\b", @"september|sept\b|sep\b", @"october|oct\b", @"november|nov\b", @"december|dec\b" };
        en.RepDaily = @"\b(?:every\s+day|everyday|daily)\b";
        en.RepWeekly = @"\b(?:every\s+week|weekly)\b";
        en.RepMonthly = @"\b(?:every\s+month|monthly)\b";
        en.RepYearly = @"\b(?:every\s+year|yearly|annually)\b";
        en.RepEveryN = $@"\bevery\s+(?<n>\d+|{nen})\s+days\b";
        en.RepWeekdayPrefix = @"\bevery\s+";
        en.WeekdayPrefix = @"(?:\b(?:on|next|this)\s+)?";
        en.Offset = $@"\bin\s+(?:(?<n>\d+|{nen})\s+)?(?<u>minutes?|mins?|hours?|hrs?|days?|weeks?)\b";
        en.TimePatterns = new[]
        {
            @"(?:\b(?:at|@)\s+)?(?<!\d)(?<h>\d{1,2})(?:[:.](?<m>\d{2}))?\s*(?<ap>a\.?m\.?|p\.?m\.?)(?!\w)",
            @"(?:\b(?:at|@)\s+)?(?<!\d)(?<h>\d{1,2}):(?<m>\d{2})(?!\d)(?:\s+in the\s+(?<p>morning|afternoon|evening|night))?",
            $@"\b(?:at|@)\s+(?<h>\d{{1,2}}|{nen})(?:\s*o'?clock)?(?:\s+in the\s+(?<p>morning|afternoon|evening|night))?"
        };
        var men = string.Join("|", en.Months);
        en.DatePatterns = new[]
        {
            $@"(?<d>\d{{1,2}})(?:st|nd|rd|th)?\s+(?:of\s+)?(?<mon>{men})",
            $@"(?<mon>{men})\s+(?<d>\d{{1,2}})(?:st|nd|rd|th)?\b"
        };
        en.Am = "morning"; en.Pm = "afternoon"; en.Evening = "evening"; en.Night = "night";
        en.LeadFiller = @"^(?:remind me to|please|i need to|i have to)\s+";
        en.EdgePreps = "at|on|in|to|the|and";

        var de = new Rules();
        var nde = NumAlt(NumDe, ExtraDe);
        de.Numbers = NumDe;
        de.Today = @"\bheute\b"; de.Tomorrow = @"\bmorgen\b"; de.DayAfter = @"\bübermorgen\b";
        de.Weekdays = new[] { @"montag\w*", @"dienstag\w*", @"mittwoch\w*", @"donnerstag\w*", @"freitag\w*", @"samstag\w*", @"sonntag\w*" };
        de.Months = new[] { @"januar\w*", @"februar\w*", @"märz\w*", @"april\w*", @"mai\b", @"juni\b", @"juli\b", @"august\w*", @"september\w*", @"oktober\w*", @"november\w*", @"dezember\w*" };
        de.RepDaily = @"\b(?:jeden\s+tag|täglich)\b";
        de.RepWeekly = @"\b(?:jede\s+woche|wöchentlich)\b";
        de.RepMonthly = @"\b(?:jeden\s+monat|monatlich)\b";
        de.RepYearly = @"\b(?:jedes\s+jahr|jährlich)\b";
        de.RepEveryN = $@"\balle\s+(?<n>\d+|{nde})\s+tage\b";
        de.RepWeekdayPrefix = @"\bjede[nrs]?\s+";
        de.WeekdayPrefix = @"(?:\b(?:am|diesen|nächsten)\s+)?";
        de.Offset = $@"\bin\s+(?:(?<n>\d+|{nde})\s+)?(?<u>minuten?|stunden?|tag(?:en|e)?|wochen?)\b";
        de.TimePatterns = new[]
        {
            @"(?:\b(?:um|gegen)\s+)?(?<!\d)(?<h>\d{1,2})[:.](?<m>\d{2})(?!\d)(?:\s*uhr)?(?:\s*(?<p>morgens|vormittags|nachmittags|abends|nachts))?",
            $@"\b(?:um|gegen)\s+(?<h>\d{{1,2}}|{nde})(?:\s*uhr)?(?:\s*(?<p>morgens|vormittags|nachmittags|abends|nachts)\b)?",
            @"(?<!\d)(?<h>\d{1,2})\s*uhr(?:\s*(?<p>morgens|vormittags|nachmittags|abends|nachts)\b)?"
        };
        var mde = string.Join("|", de.Months);
        de.DatePatterns = new[]
        {
            $@"(?<d>\d{{1,2}})\.?\s+(?<mon>{mde})"
        };
        de.Am = "morgens|vormittags"; de.Pm = "nachmittags"; de.Evening = "abends"; de.Night = "nachts";
        de.LeadFiller = @"^(?:erinnere mich(?: daran)?,?|bitte)\s+";
        de.EdgePreps = "um|am|an|in|und|zu";

        var fr = new Rules();
        var nfr = NumAlt(NumFr, ExtraFr);
        fr.Numbers = NumFr;
        fr.Today = @"\baujourd['’]hui\b"; fr.Tomorrow = @"\bdemain\b"; fr.DayAfter = @"\bapr[èe]s-demain\b";
        fr.Weekdays = new[] { @"lundis?\b", @"mardis?\b", @"mercredis?\b", @"jeudis?\b", @"vendredis?\b", @"samedis?\b", @"dimanches?\b" };
        fr.Months = new[] { @"janvier", @"f[ée]vrier", @"mars", @"avril", @"mai\b", @"juin", @"juillet", @"ao[uû]t", @"septembre", @"octobre", @"novembre", @"d[ée]cembre" };
        fr.RepDaily = @"\b(?:tous\s+les\s+jours|chaque\s+jour|quotidien(?:nement)?)\b";
        fr.RepWeekly = @"\b(?:chaque\s+semaine|toutes\s+les\s+semaines|hebdomadaire)\b";
        fr.RepMonthly = @"\b(?:chaque\s+mois|tous\s+les\s+mois|mensuel(?:lement)?)\b";
        fr.RepYearly = @"\b(?:chaque\s+ann[ée]e|tous\s+les\s+ans|annuel(?:lement)?)\b";
        fr.RepEveryN = $@"\btous\s+les\s+(?<n>\d+|{nfr})\s+jours\b";
        fr.RepWeekdayPrefix = @"\b(?:chaque|tous\s+les)\s+";
        fr.WeekdayPrefix = @"(?:\b(?:ce|le)\s+)?";
        fr.Offset = $@"\bdans\s+(?:(?<n>\d+|{nfr})\s+)?(?<u>minutes?|heures?|jours?|semaines?)\b";
        fr.TimePatterns = new[]
        {
            @"(?:\b[àa]\s+)?(?<!\d)(?<h>\d{1,2})\s*[:h]\s*(?<m>\d{2})(?!\d)(?:\s+(?:du|de l['’]?)\s*(?<p>matin|apr[èe]s-midi|soir|nuit))?",
            $@"\b[àa]\s+(?<h>\d{{1,2}}|{nfr})\s*(?:heures?|h)?(?:\s+(?:du|de l['’]?)\s*(?<p>matin|apr[èe]s-midi|soir|nuit))?(?!\w)"
        };
        var mfr = string.Join("|", fr.Months);
        fr.DatePatterns = new[]
        {
            $@"(?<d>\d{{1,2}}|1er)\s+(?<mon>{mfr})"
        };
        fr.Am = "matin"; fr.Pm = "apr[èe]s-midi"; fr.Evening = "soir"; fr.Night = "nuit";
        fr.LeadFiller = @"^(?:rappelle[- ]moi(?: de)?|il faut|s'il te pla[iî]t)\s+";
        fr.EdgePreps = "à|a|le|la|de|du|et";

        return new Dictionary<string, Rules> { ["ru"] = ru, ["en"] = en, ["de"] = de, ["fr"] = fr };
    }

    private const RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    public static ParsedTask Parse(string text, string cultureOrLang, DateTime now)
    {
        var lang = cultureOrLang.Length >= 2 ? cultureOrLang[..2].ToLowerInvariant() : "ru";
        if (!All.TryGetValue(lang, out var R)) { lang = "ru"; R = All["ru"]; }

        var res = new ParsedTask();
        string work = " " + (text ?? "").Trim() + " ";

        bool Take(string pattern, out Match m)
        {
            m = Regex.Match(work, pattern, Opt);
            if (!m.Success) return false;
            work = work.Remove(m.Index, m.Length).Insert(m.Index, " ");
            return true;
        }

        int N(string token) => lang switch
        {
            "en" => NumValue(NumEn, token, ExtraEn),
            "de" => NumValue(NumDe, token, ExtraDe),
            "fr" => NumValue(NumFr, token, ExtraFr),
            _ => NumValue(NumRu, token, ExtraRu)
        };

        DateTime? date = null;
        TimeSpan? time = null;
        bool offsetTime = false;

        // ---------- Повторение ----------
        Match m;
        if (Take(R.RepEveryN, out m))
        {
            res.Repeat = RepeatKind.EveryNDays;
            res.RepeatInterval = Math.Max(1, N(m.Groups["n"].Value));
        }
        else if (Take(R.RepDaily, out _)) res.Repeat = RepeatKind.Daily;
        else if (Take(R.RepWeekly, out _)) res.Repeat = RepeatKind.Weekly;
        else if (Take(R.RepMonthly, out _)) res.Repeat = RepeatKind.Monthly;
        else if (Take(R.RepYearly, out _)) res.Repeat = RepeatKind.Yearly;
        else
        {
            // «каждый понедельник» (возможно несколько дней)
            int mask = 0;
            for (int guard = 0; guard < 7; guard++)
            {
                bool found = false;
                for (int d = 0; d < 7; d++)
                {
                    if (Take(R.RepWeekdayPrefix + "(?:" + R.Weekdays[d] + ")", out _))
                    {
                        mask |= 1 << d;
                        found = true;
                        break;
                    }
                }
                if (!found) break;
            }
            if (mask != 0)
            {
                res.Repeat = RepeatKind.Weekdays;
                res.RepeatDays = mask;
            }
        }

        // ---------- «Через N …» ----------
        if (Take(R.Offset, out m))
        {
            int n = m.Groups["n"].Success ? N(m.Groups["n"].Value) : 1;
            if (n < 0) n = 1;
            switch (UnitKind(m.Groups["u"].Value))
            {
                case 0: { var d = now.AddMinutes(n); date = d.Date; time = new TimeSpan(d.Hour, d.Minute, 0); offsetTime = true; break; }
                case 1: { var d = now.AddHours(n); date = d.Date; time = new TimeSpan(d.Hour, d.Minute, 0); offsetTime = true; break; }
                case 2: date = now.Date.AddDays(n); break;
                default: date = now.Date.AddDays(7 * n); break;
            }
        }

        // ---------- Дата ----------
        if (date == null)
        {
            if (Take(R.DayAfter, out _)) date = now.Date.AddDays(2);
            else if (Take(R.Tomorrow, out _)) date = now.Date.AddDays(1);
            else if (Take(R.Today, out _)) date = now.Date;
        }
        if (date == null)
        {
            foreach (var pat in R.DatePatterns)
            {
                if (!Take(pat, out m)) continue;
                int day = m.Groups["d"].Value.StartsWith("1er") ? 1 : int.Parse(Regex.Match(m.Groups["d"].Value, @"\d+").Value);
                int mon = MonthIndex(R, m.Groups["mon"].Value);
                if (mon > 0 && day >= 1 && day <= 31) date = ResolveDate(now, mon, day, null);
                break;
            }
        }
        if (date == null)
        {
            // числовая дата 15.11 / 15/11/2026 — не трогаем текст, если это на самом деле время вроде 14.30
            var dm = Regex.Match(work, @"(?<!\d)(?<d>\d{1,2})[./](?<mo>\d{1,2})(?:[./](?<y>\d{2,4}))?(?![\d:])", Opt);
            if (dm.Success && int.Parse(dm.Groups["mo"].Value) is >= 1 and <= 12 && int.Parse(dm.Groups["d"].Value) is >= 1 and <= 31)
            {
                work = work.Remove(dm.Index, dm.Length).Insert(dm.Index, " ");
                int? y = dm.Groups["y"].Success ? int.Parse(dm.Groups["y"].Value) : null;
                if (y is < 100) y += 2000;
                date = ResolveDate(now, int.Parse(dm.Groups["mo"].Value), int.Parse(dm.Groups["d"].Value), y);
            }
        }
        if (date == null && res.Repeat != RepeatKind.Weekdays)
        {
            // день недели: «в пятницу», «on Friday»
            for (int d = 0; d < 7 && date == null; d++)
            {
                if (!Take(R.WeekdayPrefix + "(?:" + R.Weekdays[d] + ")", out _)) continue;
                int today = ((int)now.DayOfWeek + 6) % 7;
                int diff = (d - today + 7) % 7;
                if (diff == 0) diff = 7;
                date = now.Date.AddDays(diff);
            }
        }

        // ---------- Время ----------
        if (time == null)
        {
            foreach (var pat in R.TimePatterns)
            {
                if (!Take(pat, out m)) continue;
                int h = N(m.Groups["h"].Value);
                if (h < 0 || h > 24) continue;
                int mi = m.Groups["m"].Success ? int.Parse(m.Groups["m"].Value) : 0;
                if (mi > 59) continue;
                string p = m.Groups["p"].Success ? m.Groups["p"].Value.ToLowerInvariant() : "";
                string ap = m.Groups["ap"].Success ? m.Groups["ap"].Value.ToLowerInvariant().Replace(".", "") : "";
                bool colonForm = m.Groups["m"].Success;
                bool twelveStyle = h >= 1 && h <= 12;
                bool decided = false;

                if (ap == "am") { if (h == 12) h = 0; decided = true; }
                else if (ap == "pm") { if (h < 12) h += 12; decided = true; }
                else if (p.Length > 0)
                {
                    if (Regex.IsMatch(p, "^(?:" + R.Evening + ")$")) { if (h < 12) h += 12; }
                    else if (Regex.IsMatch(p, "^(?:" + R.Pm + ")$")) { if (h < 12) h += 12; if (h == 24) h = 12; }
                    else if (Regex.IsMatch(p, "^(?:" + R.Night + ")$")) { if (h == 12) h = 0; else if (h >= 6 && h < 12) h += 12; }
                    else { if (h == 12) h = 0; } // утро
                    decided = true;
                }
                else if (h > 12 || h == 0 || (colonForm && lang != "en") || (colonForm && m.Groups["h"].Value.StartsWith("0")))
                {
                    decided = true; // 24-часовой формат
                }

                if (!decided && twelveStyle)
                {
                    // «в три часа» — уточняем у пользователя; по умолчанию предполагаем разумное
                    res.AmbiguousHour = true;
                    if (h >= 1 && h <= 6) h += 12;
                }
                if (h == 24) h = 0;
                time = new TimeSpan(h, mi, 0);
                break;
            }
        }

        // ---------- Значения по умолчанию ----------
        if (res.Repeat == RepeatKind.Weekdays && date == null && res.RepeatDays != 0)
        {
            var t = time ?? new TimeSpan(9, 0, 0);
            for (int i = 0; i < 8; i++)
            {
                var cand = now.Date.AddDays(i);
                if ((res.RepeatDays & Recurrence.DayBit(cand.DayOfWeek)) == 0) continue;
                if (i == 0 && cand + t <= now) continue;
                date = cand;
                break;
            }
        }
        if (date == null && time != null && !offsetTime)
            date = (now.Date + time.Value) > now ? now.Date : now.Date.AddDays(1);

        res.Date = date;
        res.Time = time;
        res.Title = CleanTitle(work, R);
        return res;
    }

    /// <summary>Пользователь подтвердил, что имелось в виду другое время (AM/PM).</summary>
    public static void ApplyHour(ParsedTask t, bool pm)
    {
        if (t.Time == null) return;
        int h = t.Time.Value.Hours % 12;
        if (pm) h += 12;
        t.Time = new TimeSpan(h, t.Time.Value.Minutes, 0);
        t.AmbiguousHour = false;
    }

    private static int UnitKind(string u)
    {
        u = u.ToLowerInvariant();
        if (u.StartsWith("мин") || u.StartsWith("min")) return 0;
        if (u.StartsWith("час") || u.StartsWith("hour") || u.StartsWith("hr") || u.StartsWith("stund") || u.StartsWith("heure")) return 1;
        if (u.StartsWith("нед") || u.StartsWith("week") || u.StartsWith("woch") || u.StartsWith("semaine")) return 3;
        return 2;
    }

    private static int MonthIndex(Rules r, string token)
    {
        for (int i = 0; i < 12; i++)
            if (Regex.IsMatch(token, "^(?:" + r.Months[i] + ")", Opt)) return i + 1;
        return 0;
    }

    private static DateTime ResolveDate(DateTime now, int month, int day, int? year)
    {
        int y = year ?? now.Year;
        day = Math.Min(day, DateTime.DaysInMonth(y, month));
        var d = new DateTime(y, month, day);
        if (year == null && d < now.Date)
        {
            y++;
            d = new DateTime(y, month, Math.Min(day, DateTime.DaysInMonth(y, month)));
        }
        return d;
    }

    private static string CleanTitle(string s, Rules r)
    {
        s = Regex.Replace(s, @"\s+", " ").Trim(' ', ',', '.', ';', ':', '-', '—');
        s = Regex.Replace(s, r.LeadFiller, "", Opt).Trim();
        // убираем висящие предлоги по краям («в», «at» …)
        for (int i = 0; i < 3; i++)
        {
            var before = s;
            s = Regex.Replace(s, @"^(?:" + r.EdgePreps + @")\s+", "", Opt);
            s = Regex.Replace(s, @"\s+(?:" + r.EdgePreps + @")$", "", Opt);
            s = s.Trim(' ', ',', '.', ';', ':', '-', '—');
            if (s == before) break;
        }
        if (s.Length > 0) s = char.ToUpperInvariant(s[0]) + s[1..];
        return s;
    }
}
