using System.IO;
using System.Media;

namespace MyPlanner.Services;

/// <summary>
/// Собственный мягкий двухнотный сигнал («тыдын — тыдын»). Звук синтезируется в памяти,
/// громкость задаётся амплитудой, поэтому не зависит от внешних файлов.
/// </summary>
public static class SoundService
{
    public static readonly string[] Ids = { "soft", "bell", "marimba" };
    private const int Rate = 44100;
    private static SoundPlayer? _player;

    public static void Play(string id, double volume)
    {
        try
        {
            var wav = BuildWav(id, volume);
            _player?.Stop();
            _player?.Dispose();
            _player = new SoundPlayer(new MemoryStream(wav));
            _player.Play();
        }
        catch (Exception ex)
        {
            Logger.Write("Sound play failed", ex);
        }
    }

    public static void Stop()
    {
        try { _player?.Stop(); } catch { }
    }

    public static byte[] BuildWav(string id, double volume)
    {
        double amp = 0.08 + 0.55 * Math.Clamp(volume, 0, 1);
        // две пары нот: (ты-дын) (ты-дын)
        var motif = id switch
        {
            "bell" => new[] { (880.0, 0.22), (1174.7, 0.45) },
            "marimba" => new[] { (523.3, 0.16), (784.0, 0.30) },
            _ => new[] { (659.3, 0.20), (880.0, 0.42) } // soft: E5 -> A5
        };
        var samples = new List<short>();
        for (int rep = 0; rep < 2; rep++)
        {
            foreach (var (freq, len) in motif)
                AddNote(samples, freq, len, amp, id);
            AddSilence(samples, rep == 0 ? 0.18 : 0.05);
        }
        return ToWav(samples);
    }

    private static void AddNote(List<short> buf, double freq, double seconds, double amp, string id)
    {
        int n = (int)(seconds * Rate);
        double decay = id == "marimba" ? 9.0 : id == "bell" ? 4.2 : 5.5;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / Rate;
            double attack = Math.Min(1.0, t / 0.012);
            double env = attack * Math.Exp(-decay * t);
            double tail = Math.Min(1.0, (n - i) / (0.02 * Rate)); // плавное затухание без щелчка
            double v;
            if (id == "bell")
                v = Math.Sin(2 * Math.PI * freq * t) + 0.35 * Math.Sin(2 * Math.PI * freq * 2.76 * t) + 0.12 * Math.Sin(2 * Math.PI * freq * 5.4 * t);
            else if (id == "marimba")
                v = Math.Sin(2 * Math.PI * freq * t) + 0.25 * Math.Sin(2 * Math.PI * freq * 4.0 * t);
            else
                v = Math.Sin(2 * Math.PI * freq * t) + 0.22 * Math.Sin(2 * Math.PI * freq * 2 * t) + 0.08 * Math.Sin(2 * Math.PI * freq * 3 * t);
            v = v / 1.4 * env * tail * amp;
            buf.Add((short)(Math.Clamp(v, -1, 1) * short.MaxValue));
        }
    }

    private static void AddSilence(List<short> buf, double seconds)
    {
        int n = (int)(seconds * Rate);
        for (int i = 0; i < n; i++) buf.Add(0);
    }

    private static byte[] ToWav(List<short> samples)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        int dataLen = samples.Count * 2;
        w.Write(new[] { 'R', 'I', 'F', 'F' });
        w.Write(36 + dataLen);
        w.Write(new[] { 'W', 'A', 'V', 'E' });
        w.Write(new[] { 'f', 'm', 't', ' ' });
        w.Write(16);
        w.Write((short)1);       // PCM
        w.Write((short)1);       // моно
        w.Write(Rate);
        w.Write(Rate * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write(new[] { 'd', 'a', 't', 'a' });
        w.Write(dataLen);
        foreach (var s in samples) w.Write(s);
        w.Flush();
        return ms.ToArray();
    }
}
