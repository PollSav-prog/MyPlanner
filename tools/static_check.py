#!/usr/bin/env python3
"""
Статические проверки проекта (компилятор .NET в этой среде недоступен):
  1) корректность XML (XAML, csproj, slnx);
  2) баланс скобок в C# с учётом строк, комментариев и интерполяции;
  3) обработчики событий и x:Name из XAML существуют в code-behind;
  4) привязки {Binding ...} в MainWindow.xaml указывают на существующие свойства;
  5) обращения к статическим членам собственных классов существуют.
"""
import re, os, sys, glob
import xml.etree.ElementTree as ET

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
SRC = os.path.join(ROOT, "src", "MyPlanner")
problems = 0
def bad(msg):
    global problems
    problems += 1
    print("  ПРОБЛЕМА:", msg)

# ---------- 1. XML ----------
print("1) XML")
xml_files = glob.glob(ROOT + "/**/*.xaml", recursive=True) + glob.glob(ROOT + "/**/*.csproj", recursive=True) + glob.glob(ROOT + "/*.slnx")
for f in xml_files:
    try:
        ET.parse(f)
    except Exception as e:
        bad("%s: %s" % (os.path.relpath(f, ROOT), e))
print("   проверено файлов:", len(xml_files))

# ---------- 2. Баланс скобок C# ----------
print("2) Баланс скобок C#")
PAIRS = {")": "(", "]": "[", "}": "{"}

def scan(s, i, stop_at_brace):
    """Сканирует код; возвращает (индекс, ошибка). stop_at_brace — выход при закрывающей } на нулевой глубине (для дыр интерполяции)."""
    stack = []
    n = len(s)
    while i < n:
        c = s[i]
        if s.startswith("//", i):
            j = s.find("\n", i); i = n if j < 0 else j; continue
        if s.startswith("/*", i):
            j = s.find("*/", i + 2)
            if j < 0: return i, "незакрытый /* */"
            i = j + 2; continue
        # строки
        m = re.match(r'(\$@|@\$|\$|@)?"', s[i:i + 3])
        if c in "$@\"" and m and (c == '"' or s[i:i + 3].lstrip("$@").startswith('"') or True):
            mm = re.match(r'(\$@|@\$|\$|@)?"', s[i:])
            if mm:
                prefix = mm.group(1) or ""
                verbatim = "@" in prefix
                interp = "$" in prefix
                j = i + len(mm.group(0))
                while True:
                    if j >= n: return i, "незакрытая строка"
                    ch = s[j]
                    if verbatim and ch == '"' and s.startswith('""', j): j += 2; continue
                    if not verbatim and ch == "\\": j += 2; continue
                    if ch == '"': j += 1; break
                    if interp and ch == "{":
                        if s.startswith("{{", j): j += 2; continue
                        k, err = scan(s, j + 1, True)
                        if err: return j, err
                        j = k + 1; continue
                    if interp and ch == "}" and s.startswith("}}", j): j += 2; continue
                    if not verbatim and ch == "\n": return i, "перенос строки внутри обычной строки"
                    j += 1
                i = j; continue
        if c == "'":
            j = i + 1
            while j < n and s[j] != "'":
                j += 2 if s[j] == "\\" else 1
            i = j + 1; continue
        if c in "([{":
            stack.append(c)
        elif c in ")]}":
            if not stack:
                if stop_at_brace and c == "}":
                    return i, None
                return i, "лишняя %s" % c
            if stack[-1] != PAIRS[c]:
                return i, "несоответствие скобок: %s ... %s" % (stack[-1], c)
            stack.pop()
        i += 1
    if stop_at_brace: return i, "незакрытая дыра интерполяции"
    if stack: return i, "не закрыто: %s" % "".join(stack)
    return i, None

cs_files = glob.glob(ROOT + "/src/**/*.cs", recursive=True) + glob.glob(ROOT + "/tests/**/*.cs", recursive=True)
cs_files = [f for f in cs_files if "/obj/" not in f and "/bin/" not in f]
for f in cs_files:
    s = open(f, encoding="utf-8").read()
    pos, err = scan(s, 0, False)
    if err:
        line = s.count("\n", 0, pos) + 1
        bad("%s:%d %s" % (os.path.relpath(f, ROOT), line, err))
print("   проверено файлов:", len(cs_files))

# ---------- 3. XAML ↔ code-behind ----------
print("3) Обработчики и имена в XAML")
for xf in glob.glob(SRC + "/**/*.xaml", recursive=True):
    cf = xf + ".cs"
    if not os.path.exists(cf): continue
    x = open(xf, encoding="utf-8").read()
    c = open(cf, encoding="utf-8").read()
    for ev in re.findall(r'\b(?:Click|MouseLeftButtonUp|MouseDoubleClick|Closing|StateChanged|Loaded|SelectionChanged)="(\w+)"', x):
        if not re.search(r"\b%s\s*\(" % ev, c):
            bad("%s: обработчик %s не найден в code-behind" % (os.path.basename(xf), ev))
    for nm in re.findall(r'x:Name="(\w+)"', x):
        pass
    for nm in ("LangButton", "TaskList"):
        if "x:Name=\"%s\"" % nm not in x and nm in c:
            bad("%s: имя %s используется в коде, но не задано в XAML" % (os.path.basename(xf), nm))

# ---------- 4. Привязки MainWindow.xaml ----------
print("4) Привязки MainWindow.xaml")
def members(path):
    s = open(path, encoding="utf-8").read()
    return set(re.findall(r"public\s+(?:static\s+)?(?:readonly\s+)?[\w<>\[\],\.\?\s]+?\s+(\w+)\s*(?:\{|=>|\(|;|=)", s))
vm = members(SRC + "/ViewModels/MainViewModel.cs") | members(SRC + "/ViewModels/Base.cs")
task = members(SRC + "/Models/TaskItem.cs")
x = open(SRC + "/Views/MainWindow.xaml", encoding="utf-8").read()
known_local = {"Text", "TextOpacity", "IsSelected", "IsToday", "HasTasks", "HasDroplet", "ShowDropletCount", "DropletCount"}
for b in sorted(set(re.findall(r"\{Binding\s+(?:Path=)?([A-Za-z_][\w\.]*)(?![^}]*ElementName)", x))):
    root = b.split(".")[0]
    if root in ("DataContext",) or root in known_local or root in vm or root in task:
        continue
    bad("привязка {Binding %s} не найдена ни в MainViewModel, ни в TaskItem, ни в DayCell" % b)
for b in re.findall(r'<Binding Path="(\w+)"', x):
    if b not in task and b not in vm: bad("Binding Path=%s не найден" % b)
for key in sorted(set(re.findall(r"StaticResource (\w+)", x))):
    found = any(("x:Key=\"%s\"" % key) in open(f, encoding="utf-8").read() for f in glob.glob(SRC + "/Themes/*.xaml"))
    if not found and key != "Muted": bad("ресурс %s не найден в Themes" % key)
for key in sorted(set(re.findall(r"DynamicResource (\w+)", x))):
    found = all(("x:Key=\"%s\"" % key) in open(f, encoding="utf-8").read() for f in (SRC + "/Themes/Light.xaml", SRC + "/Themes/Dark.xaml"))
    if not found: bad("DynamicResource %s отсутствует в Light.xaml/Dark.xaml (или это ресурс Common)" % key)
# SetResourceReference из кода
for f in cs_files:
    s = open(f, encoding="utf-8").read()
    for key in re.findall(r'SetResourceReference\([^,]+,\s*"(\w+)"', s):
        if not all(("x:Key=\"%s\"" % key) in open(p, encoding="utf-8").read() for p in (SRC + "/Themes/Light.xaml", SRC + "/Themes/Dark.xaml")):
            bad("%s: ресурс %s не найден в обеих темах" % (os.path.basename(f), key))
    for key in re.findall(r'FindResource\("(\w+)"\)', s):
        ok = any(("x:Key=\"%s\"" % key) in open(p, encoding="utf-8").read() for p in glob.glob(SRC + "/Themes/*.xaml"))
        if not ok: bad("%s: FindResource(%s) не найден" % (os.path.basename(f), key))

# ---------- 5. Статические члены собственных классов ----------
print("5) Ссылки на члены собственных классов")
static_classes = {"Svc": "Services/Svc.cs", "TaskOps": "Services/Svc.cs", "Dialogs": "Views/Dialogs.cs",
                  "SoundService": "Services/SoundService.cs", "ThemeService": "Services/ThemeService.cs",
                  "AutostartService": "Services/AutostartService.cs", "QuietMode": "Services/TrayService.cs",
                  "NaturalDateParser": "Services/NaturalDateParser.cs", "Recurrence": "Services/Recurrence.cs",
                  "NotificationCoordinator": "Services/NotificationCoordinator.cs", "Logger": "Services/Logger.cs",
                  "WeatherService": "Services/WeatherService.cs"}
def class_body(path, cls):
    s = open(os.path.join(SRC, path), encoding="utf-8").read()
    i = s.find("class %s" % cls)
    return s[i:] if i >= 0 else ""
for f in cs_files:
    s = open(f, encoding="utf-8").read()
    for cls, path in static_classes.items():
        body = class_body(path, cls)
        for mem in set(re.findall(r"(?<![\w.])%s\.(\w+)" % cls, s)):
            if not re.search(r"\b%s\b" % re.escape(mem), body):
                bad("%s: %s.%s не определён" % (os.path.relpath(f, ROOT), cls, mem))
# методы/свойства Loc
loc_body = open(SRC + "/Services/Loc.cs", encoding="utf-8").read()
for f in cs_files:
    s = open(f, encoding="utf-8").read()
    for mem in set(re.findall(r"\bLoc\.I\.(\w+)", s)):
        if not re.search(r"\b%s\b" % mem, loc_body):
            bad("%s: Loc.I.%s не определён" % (os.path.relpath(f, ROOT), mem))
# AppSettings
st = open(SRC + "/Services/AppSettings.cs", encoding="utf-8").read()
for f in cs_files:
    s = open(f, encoding="utf-8").read()
    for mem in set(re.findall(r"\b(?:Svc\.Settings|S|st|s)\.([A-Z]\w+)\b", s)):
        pass
for f in cs_files:
    s = open(f, encoding="utf-8").read()
    for mem in set(re.findall(r"Svc\.Settings\.(\w+)", s)):
        if not re.search(r"\b%s\b" % mem, st):
            bad("%s: Svc.Settings.%s не определён" % (os.path.relpath(f, ROOT), mem))
# ViewModel members, вызываемые снаружи
for f in cs_files:
    s = open(f, encoding="utf-8").read()
    for mem in set(re.findall(r"ViewModel\.(\w+)", s)):
        if mem not in vm: bad("%s: ViewModel.%s не определён" % (os.path.relpath(f, ROOT), mem))

print("\nИТОГО проблем:", problems)
sys.exit(1 if problems else 0)
