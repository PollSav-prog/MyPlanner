using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MyPlanner.Models;
using MyPlanner.Services;
using MyPlanner.Views;

namespace MyPlanner.ViewModels;

public class MainViewModel : ObservableBase
{
    private List<TaskItem> _all = new();
    private DateTime _selected = DateTime.Today;
    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private TaskItem? _selectedTask;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _tick;

    public ObservableCollection<TaskItem> Tasks { get; } = new();
    public ObservableCollection<DayCell> Cells { get; } = new();
    public List<string> WeekDays { get; private set; } = new();

    public MainViewModel()
    {
        // команды
        AddCommand = new RelayCommand(AddTask);
        EditCommand = new RelayCommand(EditSelected);
        DoneCommand = new RelayCommand(DoneSelected);
        ToggleDoneCommand = new RelayCommand(p => { if (p is TaskItem t) Toggle(t); });
        DeleteCommand = new RelayCommand(p => DeleteTask((p as TaskItem) ?? SelectedTask));
        MoveCommand = new RelayCommand(p => MoveTask(SelectedTask, p as string));
        RepeatCommand = new RelayCommand(RepeatSelected);
        SoundCommand = new RelayCommand(() => OpenSettings(SettingsTab.Sound));
        QuietCommand = new RelayCommand(OpenQuiet);
        WeatherCommand = new RelayCommand(() => OpenSettings(SettingsTab.Weather));
        HistoryCommand = new RelayCommand(() => new HistoryWindow { Owner = Svc.Main }.ShowDialog());
        SettingsCommand = new RelayCommand(() => OpenSettings(SettingsTab.Appearance));
        VoiceCommand = new RelayCommand(() => new VoiceWindow(SelectedDate) { Owner = Svc.Main }.ShowDialog());
        PrevMonthCommand = new RelayCommand(() => ShiftMonth(-1));
        NextMonthCommand = new RelayCommand(() => ShiftMonth(1));
        PrevYearCommand = new RelayCommand(() => ShiftMonth(-12));
        NextYearCommand = new RelayCommand(() => ShiftMonth(12));
        TodayCommand = new RelayCommand(() => SelectDate(DateTime.Today));

        Svc.DataChanged += Reload;
        Loc.I.LanguageChanged += OnLanguageChanged;
        Svc.Weather.Changed += () => Application.Current.Dispatcher.InvokeAsync(RaiseWeather);

        _clock.Tick += OnTick;
        _clock.Start();
        BuildWeekDays();
        Reload();
        OnTick(null, EventArgs.Empty);
    }

    // ---------------- Команды ----------------
    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DoneCommand { get; }
    public ICommand ToggleDoneCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand MoveCommand { get; }
    public ICommand RepeatCommand { get; }
    public ICommand SoundCommand { get; }
    public ICommand QuietCommand { get; }
    public ICommand WeatherCommand { get; }
    public ICommand HistoryCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand VoiceCommand { get; }
    public ICommand PrevMonthCommand { get; }
    public ICommand NextMonthCommand { get; }
    public ICommand PrevYearCommand { get; }
    public ICommand NextYearCommand { get; }
    public ICommand TodayCommand { get; }

    // ---------------- Свойства ----------------
    public DateTime SelectedDate
    {
        get => _selected;
        private set
        {
            if (_selected == value) return;
            _selected = value;
            Raise(); Raise(nameof(SelectedDateText));
        }
    }

    public TaskItem? SelectedTask
    {
        get => _selectedTask;
        set => Set(ref _selectedTask, value);
    }

    public string SelectedDateText => Capitalize(SelectedDate.ToString("D", Loc.I.Culture));
    public string MonthTitle => Capitalize(_month.ToString("MMMM yyyy", Loc.I.Culture));
    public string TodayButtonText => Loc.I["btn_today"];

    private string _clockText = "", _dateLine = "";
    public string ClockText { get => _clockText; private set => Set(ref _clockText, value); }
    public string DateLine { get => _dateLine; private set => Set(ref _dateLine, value); }

    private string _wIcon = "", _wTemp = "", _wCity = "", _wInfo = "";
    public string WeatherIcon { get => _wIcon; private set => Set(ref _wIcon, value); }
    public string WeatherTemp { get => _wTemp; private set => Set(ref _wTemp, value); }
    public string WeatherCity { get => _wCity; private set => Set(ref _wCity, value); }
    public string WeatherInfo { get => _wInfo; private set => Set(ref _wInfo, value); }

    private bool _quiet;
    public bool QuietActive { get => _quiet; private set => Set(ref _quiet, value); }
    public string QuietText => QuietActive && Svc.Settings.QuietUntil is DateTime u
        ? Loc.I.F("quiet_until", u.ToString("t", Loc.I.Culture)) : "";

    // ---------------- Календарь ----------------
    private void BuildWeekDays()
    {
        var fmt = Loc.I.Culture.DateTimeFormat;
        WeekDays = new List<string>();
        for (int i = 1; i <= 7; i++)
            WeekDays.Add(Capitalize(fmt.GetAbbreviatedDayName((DayOfWeek)(i % 7))));
        Raise(nameof(WeekDays));
    }

    private void ShiftMonth(int months)
    {
        _month = _month.AddMonths(months);
        Raise(nameof(MonthTitle));
        RebuildCells();
    }

    public void SelectDate(DateTime date, TaskItem? select = null)
    {
        SelectedDate = date.Date;
        if (_month.Year != date.Year || _month.Month != date.Month)
        {
            _month = new DateTime(date.Year, date.Month, 1);
            Raise(nameof(MonthTitle));
        }
        RebuildCells();
        RebuildTasks();
        if (select != null) SelectedTask = Tasks.FirstOrDefault(t => t.Id == select.Id);
    }

    private void RebuildCells()
    {
        var now = DateTime.Now;
        var first = _month;
        int offset = ((int)first.DayOfWeek + 6) % 7; // понедельник — первый
        var start = first.AddDays(-offset);

        Cells.Clear();
        for (int i = 0; i < 42; i++)
        {
            var d = start.AddDays(i);
            var dayTasks = _all.Where(t => !t.IsArchived && t.Due.Date == d).ToList();
            var drops = dayTasks.Where(t => ShouldShowDroplet(t, now)).ToList();
            Cells.Add(new DayCell
            {
                Date = d,
                IsOtherMonth = d.Month != _month.Month,
                IsToday = d == DateTime.Today,
                IsSelected = d == SelectedDate,
                HasTasks = dayTasks.Any(t => !t.IsDone),
                HasDroplet = drops.Count > 0,
                DropletCount = drops.Count
            });
        }
    }

    /// <summary>Капелька видна от момента предупреждения до выполнения/переноса задачи.</summary>
    public static bool ShouldShowDroplet(TaskItem t, DateTime now)
    {
        if (t.IsDone || t.IsArchived || !t.RemindEnabled) return false;
        int w = Svc.Settings.EffectiveWarnMinutes(t);
        return w > 0 && now >= t.Due.AddMinutes(-w);
    }

    public List<TaskItem> DropletTasksOn(DateTime date) =>
        _all.Where(t => t.Due.Date == date.Date && ShouldShowDroplet(t, DateTime.Now)).OrderBy(t => t.Due).ToList();

    // ---------------- Данные ----------------
    public void Reload()
    {
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.InvokeAsync(Reload);
            return;
        }
        _all = Svc.Db.LoadAll();
        RebuildCells();
        RebuildTasks();
    }

    private void RebuildTasks()
    {
        long? keep = SelectedTask?.Id;
        var list = _all.Where(t => !t.IsArchived && t.Due.Date == SelectedDate)
            .OrderBy(t => t.IsDone).ThenBy(t => t.Due).ToList();
        Tasks.Clear();
        foreach (var t in list) Tasks.Add(t);
        SelectedTask = keep == null ? null : Tasks.FirstOrDefault(t => t.Id == keep);
    }

    private void OnLanguageChanged()
    {
        BuildWeekDays();
        Raise(nameof(MonthTitle));
        Raise(nameof(SelectedDateText));
        Raise(nameof(TodayButtonText));
        Raise(nameof(QuietText));
        Reload();
        RaiseWeather();
        OnTick(null, EventArgs.Empty);
    }

    // ---------------- Часы и погода ----------------
    private void OnTick(object? s, EventArgs e)
    {
        var now = DateTime.Now;
        ClockText = now.ToString("T", Loc.I.Culture);
        DateLine = Capitalize(now.ToString("D", Loc.I.Culture));

        bool q = Svc.Settings.IsQuietNow(now);
        if (q != QuietActive) { QuietActive = q; Raise(nameof(QuietText)); }

        _tick++;
        if (_tick % 15 == 0)
        {
            try { Svc.Reminders.Tick(); } catch (Exception ex) { Logger.Write("Reminder tick failed", ex); }
        }
        if (_tick % 60 == 0) RebuildCells();   // капельки появляются со временем
        if (_tick % 1800 == 0) _ = Svc.Weather.RefreshAsync();
        if (_tick % 3600 == 1) Autobackup();
    }

    public void CatchUp()
    {
        try { Svc.Reminders.Tick(catchUp: true); } catch (Exception ex) { Logger.Write("CatchUp failed", ex); }
    }

    private static void Autobackup()
    {
        var s = Svc.Settings;
        if (!s.BackupEnabled) return;
        if (DateTime.TryParse(s.LastBackup, CultureInfo.InvariantCulture, DateTimeStyles.None, out var last)
            && DateTime.Now - last < TimeSpan.FromHours(20)) return;
        try
        {
            Svc.Db.Backup(s.BackupKeep);
            s.LastBackup = DateTime.Now.ToString("s", CultureInfo.InvariantCulture);
        }
        catch (Exception ex) { Logger.Write("Backup failed", ex); }
    }

    public void RaiseWeather()
    {
        var w = Svc.Weather.Current;
        var loc = Loc.I;
        if (string.IsNullOrWhiteSpace(Svc.Settings.City))
        {
            WeatherIcon = "🌡️"; WeatherTemp = "--"; WeatherCity = loc["weather_set_city"]; WeatherInfo = "";
            return;
        }
        if (w == null || w.Code < 0)
        {
            WeatherIcon = "🌡️"; WeatherTemp = "--";
            WeatherCity = Svc.Settings.City;
            WeatherInfo = w != null && w.Code < 0 ? loc["weather_not_found"] : loc["weather_no_data"];
            return;
        }
        WeatherIcon = WeatherService.Icon(w.Code, w.IsDay);
        WeatherTemp = string.Format(CultureInfo.CurrentCulture, "{0:+0;-0;0}°C", Math.Round(w.Temp));
        WeatherCity = w.City;
        var desc = loc[WeatherService.DescriptionKey(w.Code)];
        WeatherInfo = Svc.Weather.LastRefreshFailed
            ? loc.F("weather_offline", desc, w.Updated.ToString("g", loc.Culture))
            : desc;
    }

    // ---------------- Действия ----------------
    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], Loc.I.Culture) + s[1..];

    private void AddTask()
    {
        var t = new TaskItem { Due = SelectedDate + DefaultTime(SelectedDate) };
        var dlg = new TaskDialog(t) { Owner = Svc.Main };
        if (dlg.ShowDialog() == true) SelectDate(t.Due.Date, t);
    }

    private static TimeSpan DefaultTime(DateTime date)
    {
        if (date.Date != DateTime.Today) return new TimeSpan(9, 0, 0);
        var next = DateTime.Now.AddHours(1);
        return new TimeSpan(next.Hour, 0, 0);
    }

    private void EditSelected()
    {
        if (SelectedTask == null) { Dialogs.Info(Loc.I["msg_select_task"]); return; }
        EditTask(SelectedTask);
    }

    public void EditTask(TaskItem task)
    {
        var copy = task.Clone();
        var dlg = new TaskDialog(copy) { Owner = Svc.Main };
        if (dlg.ShowDialog() == true) SelectDate(copy.Due.Date, copy);
    }

    private void DoneSelected()
    {
        if (SelectedTask == null) { Dialogs.Info(Loc.I["msg_select_task"]); return; }
        Toggle(SelectedTask);
    }

    private void Toggle(TaskItem t)
    {
        if (t.IsDone) TaskOps.Restore(t); else TaskOps.Complete(t);
    }

    private void DeleteTask(TaskItem? t)
    {
        if (t == null) { Dialogs.Info(Loc.I["msg_select_task"]); return; }
        if (!Dialogs.Confirm(Loc.I.F("msg_confirm_delete", t.Title))) return;
        TaskOps.Archive(t);
    }

    private void MoveTask(TaskItem? t, string? where)
    {
        if (t == null) return;
        var target = where switch
        {
            "today" => DateTime.Today,
            "tomorrow" => DateTime.Today.AddDays(1),
            "week" => t.Due.Date.AddDays(7),
            _ => t.Due.Date
        };
        TaskOps.MoveToDate(t, target);
    }

    private void RepeatSelected()
    {
        if (SelectedTask == null) { Dialogs.Info(Loc.I["msg_select_task"]); return; }
        var copy = SelectedTask.Clone();
        var dlg = new RepeatDialog(copy) { Owner = Svc.Main };
        if (dlg.ShowDialog() == true) TaskOps.Save(copy);
    }

    private void OpenQuiet() => new QuietDialog { Owner = Svc.Main }.ShowDialog();

    public void OpenSettings(SettingsTab tab) => new SettingsWindow(tab) { Owner = Svc.Main }.ShowDialog();
}
