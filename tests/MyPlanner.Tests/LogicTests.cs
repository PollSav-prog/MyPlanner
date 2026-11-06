using MyPlanner.Models;
using MyPlanner.Services;
using Xunit;

namespace MyPlanner.Tests;

public class RecurrenceTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 10, 0, 0);

    [Fact]
    public void Daily_NextIsTomorrow()
    {
        var t = new TaskItem { Due = new DateTime(2026, 11, 4, 9, 0, 0), Repeat = RepeatKind.Daily };
        Assert.Equal(new DateTime(2026, 11, 5, 9, 0, 0), Recurrence.Next(t, Now));
    }

    [Fact]
    public void Daily_SkipsToFutureWhenFarOverdue()
    {
        var t = new TaskItem { Due = new DateTime(2026, 10, 20, 9, 0, 0), Repeat = RepeatKind.Daily };
        Assert.Equal(new DateTime(2026, 11, 5, 9, 0, 0), Recurrence.Next(t, Now));
    }

    [Fact]
    public void Weekdays_PicksNextSelectedDay()
    {
        // Пн + Пт; 4 ноября 2026 — среда, значит дальше пятница, 6 ноября
        var t = new TaskItem { Due = Now, Repeat = RepeatKind.Weekdays, RepeatDays = 1 | 16 };
        Assert.Equal(new DateTime(2026, 11, 6, 10, 0, 0), Recurrence.Next(t, Now));
    }

    [Fact]
    public void EveryNDays_Adds()
    {
        var t = new TaskItem { Due = Now, Repeat = RepeatKind.EveryNDays, RepeatInterval = 3 };
        Assert.Equal(Now.AddDays(3), Recurrence.Next(t, Now));
    }

    [Fact]
    public void Monthly_And_Yearly()
    {
        var m = new TaskItem { Due = new DateTime(2026, 11, 4, 12, 0, 0), Repeat = RepeatKind.Monthly };
        Assert.Equal(new DateTime(2026, 12, 4, 12, 0, 0), Recurrence.Next(m, Now));
        var y = new TaskItem { Due = new DateTime(2026, 11, 4, 12, 0, 0), Repeat = RepeatKind.Yearly };
        Assert.Equal(new DateTime(2027, 11, 4, 12, 0, 0), Recurrence.Next(y, Now));
    }

    [Fact]
    public void NoRepeat_ReturnsNull() =>
        Assert.Null(Recurrence.Next(new TaskItem { Due = Now }, Now));
}

public class SoundTests
{
    [Theory]
    [InlineData("soft")]
    [InlineData("bell")]
    [InlineData("marimba")]
    public void Wav_IsValidAndNotSilent(string id)
    {
        var wav = SoundService.BuildWav(id, 0.6);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
        Assert.True(wav.Length > 44 + 44100);           // длиннее секунды
        bool nonZero = false;
        for (int i = 44; i < wav.Length - 1; i += 2)
            if (BitConverter.ToInt16(wav, i) != 0) { nonZero = true; break; }
        Assert.True(nonZero);
    }

    [Fact]
    public void Volume_ScalesAmplitude()
    {
        static int Peak(byte[] w)
        {
            int p = 0;
            for (int i = 44; i < w.Length - 1; i += 2) p = Math.Max(p, Math.Abs((int)BitConverter.ToInt16(w, i)));
            return p;
        }
        Assert.True(Peak(SoundService.BuildWav("soft", 0.1)) < Peak(SoundService.BuildWav("soft", 1.0)));
    }
}
