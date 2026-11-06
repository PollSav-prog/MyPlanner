#!/usr/bin/env python3
"""
Проверка логики NaturalDateParser без .NET: регулярные выражения берутся ПРЯМО из C#-файла,
алгоритм Parse() перенесён 1:1 на Python. Сценарии — те же, что в ParserTests.cs.
"""
import re, sys, os, datetime as dt

SRC = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "MyPlanner", "Services", "NaturalDateParser.cs")
cs = open(SRC, encoding="utf-8").read()

# ---- числа ----
def arr(name):
    m = re.search(r"static readonly string\[\] %s = \{(.*?)\};" % name, cs, re.S)
    return re.findall(r'"([^"]+)"', m.group(1))
def extra(name):
    m = re.search(r"%s = \{(.*?)\};" % name, cs, re.S)
    return [(a, int(b)) for a, b in re.findall(r'\("([^"]+)", (\d+)\)', m.group(1))]

NUM = {"ru": arr("NumRu"), "en": arr("NumEn"), "de": arr("NumDe"), "fr": arr("NumFr")}
EXTRA = {"ru": extra("ExtraRu"), "en": extra("ExtraEn"), "de": extra("ExtraDe"), "fr": extra("ExtraFr")}

def num_alt(lang):
    words = list(NUM[lang]) + [w for w, _ in EXTRA[lang]]
    words = sorted(set(words), key=len, reverse=True)
    return "|".join(re.escape(w) for w in words)

def num_value(lang, token):
    token = re.sub(r"\s+", " ", token.strip().lower())
    if token.isdigit():
        return int(token)
    if token in NUM[lang]:
        return NUM[lang].index(token)
    for w, v in EXTRA[lang]:
        if w == token:
            return v
    return -1

def conv(p):  # .NET -> Python синтаксис именованных групп
    return re.sub(r"\(\?<([A-Za-z]\w*)>", r"(?P<\1>", p)

# ---- правила из C# ----
def lit(s):
    return s.replace('""', '"')

RULES = {}
for lang in ("ru", "en", "de", "fr"):
    start = cs.index("var %s = new Rules();" % lang)
    nxt = [cs.find("var %s = new Rules();" % l, start + 10) for l in ("en", "de", "fr")]
    nxt = [n for n in nxt if n > 0]
    end = min(nxt) if nxt else cs.index("return new Dictionary<string, Rules>")
    block = cs[start:end]
    R = {}
    nalt = num_alt(lang)
    for stmt in re.split(r";\s*(?=%s\.\w+\s*=)" % lang, block):
        m = re.match(r"\s*%s\.(\w+)\s*=\s*(.*)$" % lang, stmt, re.S)
        if not m:
            continue
        field, rhs = m.group(1), m.group(2)
        if field == "Numbers":
            continue
        strs = []
        for dollar, body in re.findall(r'(\$?)@"((?:[^"]|"")*)"', rhs):
            body = lit(body)
            if dollar:
                body = body.replace("{{", "\x00L").replace("}}", "\x00R")
                body = body.replace("{nru}", nalt).replace("{nen}", nalt).replace("{nde}", nalt).replace("{nfr}", nalt)
                body = re.sub(r"\{m(?:ru|en|de|fr)\}", "@@MONTHS@@", body)
                body = body.replace("\x00L", "{").replace("\x00R", "}")
            strs.append(body)
        R[field] = strs if rhs.strip().startswith("new[]") else (strs[0] if strs else rhs)
    # месяцы подставляем после чтения Months
    months = R["Months"]
    for k in ("TimePatterns", "DatePatterns"):
        R[k] = [s.replace("@@MONTHS@@", "|".join(months)) for s in R[k]] if k in R else []
    RULES[lang] = R

DAYBIT = lambda d: 1 << ((d.weekday()))  # Python: Пн=0

def parse(text, culture, now):
    lang = culture[:2].lower()
    if lang not in RULES:
        lang = "ru"
    R = RULES[lang]
    res = dict(title="", date=None, time=None, amb=False, repeat="None", interval=1, days=0)
    work = " " + (text or "").strip() + " "

    def take(pattern):
        nonlocal work
        m = re.search(conv(pattern), work, re.I)
        if not m:
            return None
        work = work[:m.start()] + " " + work[m.end():]
        return m

    N = lambda tok: num_value(lang, tok)
    date = None
    time = None
    offset_time = False

    m = take(R["RepEveryN"])
    if m:
        res["repeat"] = "EveryNDays"; res["interval"] = max(1, N(m.group("n")))
    elif take(R["RepDaily"]): res["repeat"] = "Daily"
    elif take(R["RepWeekly"]): res["repeat"] = "Weekly"
    elif take(R["RepMonthly"]): res["repeat"] = "Monthly"
    elif take(R["RepYearly"]): res["repeat"] = "Yearly"
    else:
        mask = 0
        for _ in range(7):
            found = False
            for d in range(7):
                if take(R["RepWeekdayPrefix"] + "(?:" + R["Weekdays"][d] + ")"):
                    mask |= 1 << d; found = True; break
            if not found: break
        if mask:
            res["repeat"] = "Weekdays"; res["days"] = mask

    m = take(R["Offset"])
    if m:
        n = N(m.group("n")) if m.group("n") else 1
        if n < 0: n = 1
        u = m.group("u").lower()
        if u.startswith("мин") or u.startswith("min"): k = 0
        elif u.startswith(("час", "hour", "hr", "stund", "heure")): k = 1
        elif u.startswith(("нед", "week", "woch", "semaine")): k = 3
        else: k = 2
        if k == 0:
            d = now + dt.timedelta(minutes=n); date = d.date(); time = (d.hour, d.minute); offset_time = True
        elif k == 1:
            d = now + dt.timedelta(hours=n); date = d.date(); time = (d.hour, d.minute); offset_time = True
        elif k == 2:
            date = now.date() + dt.timedelta(days=n)
        else:
            date = now.date() + dt.timedelta(days=7 * n)

    if date is None:
        if take(R["DayAfter"]): date = now.date() + dt.timedelta(days=2)
        elif take(R["Tomorrow"]): date = now.date() + dt.timedelta(days=1)
        elif take(R["Today"]): date = now.date()
    if date is None:
        for pat in R["DatePatterns"]:
            m = take(pat)
            if not m: continue
            ds = m.group("d")
            day = 1 if ds.startswith("1er") else int(re.search(r"\d+", ds).group())
            mon = 0
            for i in range(12):
                if re.match("(?:" + conv(R["Months"][i]) + ")", m.group("mon"), re.I):
                    mon = i + 1; break
            if mon > 0 and 1 <= day <= 31:
                date = resolve(now, mon, day, None)
            break
    if date is None:
        # как в C#: сначала поиск без удаления, чтобы «14.30» осталось временем
        m = re.search(conv(r"(?<!\d)(?P<d>\d{1,2})[./](?P<mo>\d{1,2})(?:[./](?P<y>\d{2,4}))?(?![\d:])"), work, re.I)
        if m and 1 <= int(m.group("mo")) <= 12 and 1 <= int(m.group("d")) <= 31:
            work = work[:m.start()] + " " + work[m.end():]
            y = int(m.group("y")) if m.group("y") else None
            if y is not None and y < 100: y += 2000
            date = resolve(now, int(m.group("mo")), int(m.group("d")), y)
    if date is None and res["repeat"] != "Weekdays":
        for d in range(7):
            if date is not None: break
            if not take(R["WeekdayPrefix"] + "(?:" + R["Weekdays"][d] + ")"): continue
            today = now.weekday()
            diff = (d - today + 7) % 7
            if diff == 0: diff = 7
            date = now.date() + dt.timedelta(days=diff)

    if time is None:
        for pat in R["TimePatterns"]:
            m = take(pat)
            if not m: continue
            gd = m.groupdict()
            h = N(gd["h"])
            if h < 0 or h > 24: continue
            mi = int(gd["m"]) if gd.get("m") else 0
            if mi > 59: continue
            p = (gd.get("p") or "").lower()
            ap = (gd.get("ap") or "").lower().replace(".", "")
            colon = bool(gd.get("m"))
            twelve = 1 <= h <= 12
            decided = False
            if ap == "am":
                if h == 12: h = 0
                decided = True
            elif ap == "pm":
                if h < 12: h += 12
                decided = True
            elif p:
                if re.fullmatch("(?:" + R["Evening"] + ")", p, re.I):
                    if h < 12: h += 12
                elif re.fullmatch("(?:" + R["Pm"] + ")", p, re.I):
                    if h < 12: h += 12
                    if h == 24: h = 12
                elif re.fullmatch("(?:" + R["Night"] + ")", p, re.I):
                    if h == 12: h = 0
                    elif 6 <= h < 12: h += 12
                else:
                    if h == 12: h = 0
                decided = True
            elif h > 12 or h == 0 or (colon and lang != "en") or (colon and gd["h"].startswith("0")):
                decided = True
            if not decided and twelve:
                res["amb"] = True
                if 1 <= h <= 6: h += 12
            if h == 24: h = 0
            time = (h, mi)
            break

    if res["repeat"] == "Weekdays" and date is None and res["days"]:
        t = time or (9, 0)
        for i in range(8):
            cand = now.date() + dt.timedelta(days=i)
            if (res["days"] & DAYBIT(cand)) == 0: continue
            if i == 0 and dt.datetime.combine(cand, dt.time(*t)) <= now: continue
            date = cand; break
    if date is None and time is not None and not offset_time:
        cand = dt.datetime.combine(now.date(), dt.time(*time))
        date = now.date() if cand > now else now.date() + dt.timedelta(days=1)

    res["date"], res["time"] = date, time
    res["title"] = clean_title(work, R)
    return res

def resolve(now, month, day, year):
    import calendar
    y = year or now.year
    day = min(day, calendar.monthrange(y, month)[1])
    d = dt.date(y, month, day)
    if year is None and d < now.date():
        y += 1
        d = dt.date(y, month, min(day, calendar.monthrange(y, month)[1]))
    return d

def clean_title(s, R):
    s = re.sub(r"\s+", " ", s).strip(" ,.;:-—")
    s = re.sub(conv(R["LeadFiller"]), "", s, flags=re.I).strip()
    for _ in range(3):
        before = s
        s = re.sub(r"^(?:" + R["EdgePreps"] + r")\s+", "", s, flags=re.I)
        s = re.sub(r"\s+(?:" + R["EdgePreps"] + r")$", "", s, flags=re.I)
        s = s.strip(" ,.;:-—")
        if s == before: break
    if s: s = s[0].upper() + s[1:]
    return s

# ---- сценарии (как в ParserTests.cs) ----
NOW = dt.datetime(2026, 11, 4, 10, 0)  # среда
D = dt.date
fails = 0
def check(name, text, culture, **exp):
    global fails
    r = parse(text, culture, NOW)
    bad = []
    for k, v in exp.items():
        if r[k] != v:
            bad.append("%s: ожидалось %r, получено %r" % (k, v, r[k]))
    status = "OK  " if not bad else "FAIL"
    if bad: fails += 1
    print(status, name, "|", r["title"], r["date"], r["time"], r["repeat"], "amb" if r["amb"] else "")
    for b in bad: print("     ", b)

check("ru завтра в три часа", "Завтра в три часа позвонить врачу", "ru-RU", title="Позвонить врачу", date=D(2026, 11, 5), amb=True)
check("ru пятница десять утра", "В пятницу в десять утра сходить в магазин", "ru-RU", title="Сходить в магазин", date=D(2026, 11, 6), time=(10, 0), amb=False)
check("ru через два дня 14:30", "Через два дня в 14:30 записаться на приём", "ru-RU", title="Записаться на приём", date=D(2026, 11, 6), time=(14, 30), amb=False)
check("ru каждый понедельник", "Каждый понедельник в девять утра проверить почту", "ru-RU", title="Проверить почту", repeat="Weekdays", days=1, time=(9, 0), date=D(2026, 11, 9))
check("ru каждый день", "Каждый день в 8:00 пить воду", "ru-RU", repeat="Daily", title="Пить воду", time=(8, 0))
check("ru каждые 3 дня", "Каждые 3 дня в 20:00 поливать цветы", "ru-RU", repeat="EveryNDays", interval=3, time=(20, 0))
check("ru 15 ноября", "15 ноября в 18:00 день рождения мамы", "ru-RU", date=D(2026, 11, 15), time=(18, 0), title="День рождения мамы")
check("ru через час", "Через час выключить духовку", "ru-RU", date=D(2026, 11, 4), time=(11, 0), title="Выключить духовку")
check("ru без даты", "Купить хлеб", "ru-RU", title="Купить хлеб", date=None, time=None)
check("en tomorrow 3 pm", "Tomorrow at 3 pm call the doctor", "en-US", title="Call the doctor", date=D(2026, 11, 5), time=(15, 0), amb=False)
check("en every monday 9am", "Every Monday at 9 am check email", "en-US", title="Check email", repeat="Weekdays", time=(9, 0))
check("en in two days 14:30", "In two days at 14:30 book an appointment", "en-US", date=D(2026, 11, 6), title="Book an appointment")
check("de morgen 15 uhr", "Morgen um 15 Uhr Arzt anrufen", "de-DE", title="Arzt anrufen", date=D(2026, 11, 5), time=(15, 0))
check("de jeden montag", "Jeden Montag um 9 Uhr morgens E-Mails prüfen", "de-DE", repeat="Weekdays", time=(9, 0), title="E-Mails prüfen")
check("fr demain 15h", "Demain à 15h appeler le médecin", "fr-FR", title="Appeler le médecin", date=D(2026, 11, 5), time=(15, 0))
check("fr chaque lundi", "Chaque lundi à 9 heures du matin vérifier les e-mails", "fr-FR", repeat="Weekdays", time=(9, 0))

# --- дополнительные сценарии (те же, что в ParserTests.cs) ---
check("ru 14.30 — время, не дата", "сегодня в 14.30 встреча", "ru-RU", title="Встреча", time=(14, 30))
check("ru дата 15.11", "15.11 в 18:00 день рождения", "ru-RU", date=D(2026, 11, 15), time=(18, 0), title="День рождения")
check("ru без времени", "завтра купить хлеб", "ru-RU", title="Купить хлеб", date=D(2026, 11, 5), time=None)
check("ru через 2 часа", "через 2 часа выпить воды", "ru-RU", date=D(2026, 11, 4), time=(12, 0), title="Выпить воды")
check("ru каждые 3 дня (8:00)", "каждые 3 дня поливать цветы в 8:00", "ru-RU", repeat="EveryNDays", interval=3)
check("en every friday 9 am", "every friday at 9 am send report", "en-US", repeat="Weekdays", days=16, time=(9, 0))

print("\nПРОВАЛОВ:", fails)
sys.exit(1 if fails else 0)
