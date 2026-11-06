using System.IO;

namespace MyPlanner.Services;

public static class Logger
{
    private static readonly object Gate = new();

    public static void Write(string message, Exception? ex = null)
    {
        try
        {
            Directory.CreateDirectory(Database.DataDir);
            var path = Path.Combine(Database.DataDir, "log.txt");
            lock (Gate)
            {
                if (File.Exists(path) && new FileInfo(path).Length > 512 * 1024)
                    File.Delete(path);
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{(ex != null ? "\n" + ex : "")}\n");
            }
        }
        catch { /* журнал не должен ронять программу */ }
    }
}
