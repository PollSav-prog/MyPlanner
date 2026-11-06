using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using MyPlanner.Models;

namespace MyPlanner.Services;

/// <summary>Локальная база SQLite. Каждая операция сразу пишется на диск (автосохранение).</summary>
public sealed class Database
{
    /// <summary>Папка данных. Для тестов можно переопределить переменной среды MYPLANNER_DATA.</summary>
    public static string DataDir { get; } =
        Environment.GetEnvironmentVariable("MYPLANNER_DATA") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyPlanner");

    public string DbPath { get; } = Path.Combine(DataDir, "planner.db");
    public string BackupDir { get; } = Path.Combine(DataDir, "Backups");
    private string ConnStr => $"Data Source={DbPath};Pooling=False";

    private const string DateFmt = "yyyy-MM-dd HH:mm:ss";

    public Database()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(BackupDir);
        Init();
    }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(ConnStr);
        c.Open();
        return c;
    }

    private void Init()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Tasks(
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Title TEXT NOT NULL, Description TEXT, Due TEXT NOT NULL,
  Priority INTEGER, Category TEXT,
  IsDone INTEGER, IsArchived INTEGER, CompletedAt TEXT,
  RemindEnabled INTEGER, WarnMinutes INTEGER,
  Repeat INTEGER, RepeatInterval INTEGER, RepeatDays INTEGER,
  SnoozeUntil TEXT, LastSignal TEXT, Warned INTEGER, Signaled INTEGER);
CREATE TABLE IF NOT EXISTS Settings(Key TEXT PRIMARY KEY, Value TEXT);
CREATE INDEX IF NOT EXISTS IX_Tasks_Due ON Tasks(Due);";
        cmd.ExecuteNonQuery();
    }

    // ---------------- Задачи ----------------
    public List<TaskItem> LoadAll()
    {
        var list = new List<TaskItem>();
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT * FROM Tasks ORDER BY Due, Id";
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(Read(r));
        return list;
    }

    public TaskItem? Get(long id)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT * FROM Tasks WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? Read(r) : null;
    }

    private static TaskItem Read(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(r.GetOrdinal("Id")),
        Title = Str(r, "Title"),
        Description = Str(r, "Description"),
        Due = ParseDate(Str(r, "Due")) ?? DateTime.Now,
        Priority = Int(r, "Priority", 1),
        Category = Str(r, "Category"),
        IsDone = Int(r, "IsDone") == 1,
        IsArchived = Int(r, "IsArchived") == 1,
        CompletedAt = ParseDate(Str(r, "CompletedAt")),
        RemindEnabled = Int(r, "RemindEnabled", 1) == 1,
        WarnMinutes = Int(r, "WarnMinutes", -1),
        Repeat = (RepeatKind)Int(r, "Repeat"),
        RepeatInterval = Int(r, "RepeatInterval", 1),
        RepeatDays = Int(r, "RepeatDays"),
        SnoozeUntil = ParseDate(Str(r, "SnoozeUntil")),
        LastSignal = ParseDate(Str(r, "LastSignal")),
        Warned = Int(r, "Warned") == 1,
        Signaled = Int(r, "Signaled") == 1
    };

    private static string Str(SqliteDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? "" : r.GetString(i);
    }

    private static int Int(SqliteDataReader r, string col, int def = 0)
    {
        int i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? def : r.GetInt32(i);
    }

    private static DateTime? ParseDate(string s) =>
        DateTime.TryParseExact(s, DateFmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static string? Fmt(DateTime? d) => d?.ToString(DateFmt, CultureInfo.InvariantCulture);

    private static void Bind(SqliteCommand cmd, TaskItem t)
    {
        cmd.Parameters.AddWithValue("$title", t.Title);
        cmd.Parameters.AddWithValue("$desc", t.Description ?? "");
        cmd.Parameters.AddWithValue("$due", Fmt(t.Due));
        cmd.Parameters.AddWithValue("$prio", t.Priority);
        cmd.Parameters.AddWithValue("$cat", t.Category ?? "");
        cmd.Parameters.AddWithValue("$done", t.IsDone ? 1 : 0);
        cmd.Parameters.AddWithValue("$arch", t.IsArchived ? 1 : 0);
        cmd.Parameters.AddWithValue("$comp", (object?)Fmt(t.CompletedAt) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$rem", t.RemindEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("$warn", t.WarnMinutes);
        cmd.Parameters.AddWithValue("$rep", (int)t.Repeat);
        cmd.Parameters.AddWithValue("$repi", t.RepeatInterval);
        cmd.Parameters.AddWithValue("$repd", t.RepeatDays);
        cmd.Parameters.AddWithValue("$snz", (object?)Fmt(t.SnoozeUntil) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$last", (object?)Fmt(t.LastSignal) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$warned", t.Warned ? 1 : 0);
        cmd.Parameters.AddWithValue("$sig", t.Signaled ? 1 : 0);
    }

    public long Insert(TaskItem t)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"INSERT INTO Tasks(Title,Description,Due,Priority,Category,IsDone,IsArchived,CompletedAt,
RemindEnabled,WarnMinutes,Repeat,RepeatInterval,RepeatDays,SnoozeUntil,LastSignal,Warned,Signaled)
VALUES($title,$desc,$due,$prio,$cat,$done,$arch,$comp,$rem,$warn,$rep,$repi,$repd,$snz,$last,$warned,$sig);
SELECT last_insert_rowid();";
        Bind(cmd, t);
        t.Id = Convert.ToInt64(cmd.ExecuteScalar());
        return t.Id;
    }

    public void Update(TaskItem t)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"UPDATE Tasks SET Title=$title,Description=$desc,Due=$due,Priority=$prio,Category=$cat,
IsDone=$done,IsArchived=$arch,CompletedAt=$comp,RemindEnabled=$rem,WarnMinutes=$warn,Repeat=$rep,
RepeatInterval=$repi,RepeatDays=$repd,SnoozeUntil=$snz,LastSignal=$last,Warned=$warned,Signaled=$sig WHERE Id=$id";
        Bind(cmd, t);
        cmd.Parameters.AddWithValue("$id", t.Id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM Tasks WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    // ---------------- Настройки ----------------
    public Dictionary<string, string> LoadSettings()
    {
        var d = new Dictionary<string, string>();
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Key, Value FROM Settings";
        using var r = cmd.ExecuteReader();
        while (r.Read()) d[r.GetString(0)] = r.IsDBNull(1) ? "" : r.GetString(1);
        return d;
    }

    public void SaveSetting(string key, string value)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO Settings(Key,Value) VALUES($k,$v) ON CONFLICT(Key) DO UPDATE SET Value=$v";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }

    // ---------------- Резервные копии ----------------
    public string Backup(int keep = 10)
    {
        Directory.CreateDirectory(BackupDir);
        var file = Path.Combine(BackupDir, $"planner-{DateTime.Now:yyyyMMdd-HHmmss}.db");
        using (var src = Open())
        using (var dst = new SqliteConnection($"Data Source={file};Pooling=False"))
        {
            dst.Open();
            src.BackupDatabase(dst);
        }
        foreach (var old in Directory.GetFiles(BackupDir, "planner-*.db").OrderByDescending(f => f).Skip(keep))
        {
            try { File.Delete(old); } catch { }
        }
        return file;
    }

    public void Restore(string backupFile)
    {
        SqliteConnection.ClearAllPools();
        File.Copy(backupFile, DbPath, true);
        Init();
    }
}
