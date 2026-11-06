using MyPlanner.Models;
using MyPlanner.Services;
using Xunit;

namespace MyPlanner.Tests;

public class ParserTests
{
    // Среда, 4 ноября 2026, 10:00
    private static readonly DateTime Now = new(2026, 11, 4, 10, 0, 0);

    [Fact]
    public void Ru_TomorrowAtThree_IsAmbiguous()
    {
        var p = NaturalDateParser.Parse("Завтра в три часа позвонить врачу", "ru-RU", Now);
        Assert.Equal("Позвонить врачу", p.Title);
        Assert.Equal(new DateTime(2026, 11, 5), p.Date);
        Assert.True(p.AmbiguousHour);
        Assert.Equal(new TimeSpan(15, 0, 0), p.Time);   // предположение: 15:00, но пользователя спросят
    }

    [Fact]
    public void Ru_ApplyHour_SwitchesToMorning()
    {
        var p = NaturalDateParser.Parse("Завтра в три часа позвонить врачу", "ru-RU", Now);
        NaturalDateParser.ApplyHour(p, pm: false);
        Assert.Equal(new TimeSpan(3, 0, 0), p.Time);
        Assert.False(p.AmbiguousHour);
    }

    [Fact]
    public void Ru_FridayTenMorning()
    {
        var p = NaturalDateParser.Parse("В пятницу в десять утра сходить в магазин", "ru-RU", Now);
        Assert.Equal("Сходить в магазин", p.Title);
        Assert.Equal(new DateTime(2026, 11, 6), p.Date);
        Assert.Equal(new TimeSpan(10, 0, 0), p.Time);
        Assert.False(p.AmbiguousHour);
    }

    [Fact]
    public void Ru_InTwoDaysWithClockTime()
    {
        var p = NaturalDateParser.Parse("Через два дня в 14:30 записаться на приём", "ru-RU", Now);
        Assert.Equal("Записаться на приём", p.Title);
        Assert.Equal(new DateTime(2026, 11, 6), p.Date);
        Assert.Equal(new TimeSpan(14, 30, 0), p.Time);
        Assert.False(p.AmbiguousHour);
    }

    [Fact]
    public void Ru_EveryMonday()
    {
        var p = NaturalDateParser.Parse("Каждый понедельник в девять утра проверить почту", "ru-RU", Now);
        Assert.Equal("Проверить почту", p.Title);
        Assert.Equal(RepeatKind.Weekdays, p.Repeat);
        Assert.Equal(1, p.RepeatDays);                    // Пн = бит 0
        Assert.Equal(new TimeSpan(9, 0, 0), p.Time);
        Assert.Equal(new DateTime(2026, 11, 9), p.Date);  // ближайший понедельник
    }

    [Fact]
    public void Ru_DottedTimeIsNotADate()
    {
        var p = NaturalDateParser.Parse("сегодня в 14.30 встреча", "ru-RU", Now);
        Assert.Equal(new TimeSpan(14, 30, 0), p.Time);
        Assert.Equal("Встреча", p.Title);
    }

    [Fact]
    public void Ru_ExplicitDate()
    {
        var p = NaturalDateParser.Parse("15 ноября в 18:00 день рождения", "ru-RU", Now);
        Assert.Equal(new DateTime(2026, 11, 15), p.Date);
        Assert.Equal(new TimeSpan(18, 0, 0), p.Time);
        Assert.Equal("День рождения", p.Title);
    }

    [Fact]
    public void Ru_EveryNDays()
    {
        var p = NaturalDateParser.Parse("каждые 3 дня поливать цветы в 8:00", "ru-RU", Now);
        Assert.Equal(RepeatKind.EveryNDays, p.Repeat);
        Assert.Equal(3, p.RepeatInterval);
    }

    [Fact]
    public void En_TomorrowThreePm()
    {
        var p = NaturalDateParser.Parse("Tomorrow at 3 pm call the doctor", "en-US", Now);
        Assert.Equal("Call the doctor", p.Title);
        Assert.Equal(new DateTime(2026, 11, 5), p.Date);
        Assert.Equal(new TimeSpan(15, 0, 0), p.Time);
        Assert.False(p.AmbiguousHour);
    }

    [Fact]
    public void En_EveryFridayAtNine()
    {
        var p = NaturalDateParser.Parse("every friday at 9 am send report", "en-US", Now);
        Assert.Equal(RepeatKind.Weekdays, p.Repeat);
        Assert.Equal(16, p.RepeatDays);                   // Пт = бит 4
        Assert.Equal(new TimeSpan(9, 0, 0), p.Time);
    }

    [Fact]
    public void De_TomorrowAt15Uhr()
    {
        var p = NaturalDateParser.Parse("Morgen um 15 Uhr Arzt anrufen", "de-DE", Now);
        Assert.Equal("Arzt anrufen", p.Title);
        Assert.Equal(new DateTime(2026, 11, 5), p.Date);
        Assert.Equal(new TimeSpan(15, 0, 0), p.Time);
    }

    [Fact]
    public void Fr_TomorrowAt15h()
    {
        var p = NaturalDateParser.Parse("Demain à 15h appeler le médecin", "fr-FR", Now);
        Assert.Equal("Appeler le médecin", p.Title);
        Assert.Equal(new DateTime(2026, 11, 5), p.Date);
        Assert.Equal(new TimeSpan(15, 0, 0), p.Time);
    }

    [Fact]
    public void NoTime_UsesDefaultNine()
    {
        var p = NaturalDateParser.Parse("завтра купить хлеб", "ru-RU", Now);
        Assert.False(p.HasTime);
        Assert.Equal(new DateTime(2026, 11, 5, 9, 0, 0), p.Due);
    }

    [Fact]
    public void InHours_IsRelativeToNow()
    {
        var p = NaturalDateParser.Parse("через 2 часа выпить воды", "ru-RU", Now);
        Assert.Equal(new DateTime(2026, 11, 4, 12, 0, 0), p.Due);
        Assert.Equal("Выпить воды", p.Title);
    }
}
