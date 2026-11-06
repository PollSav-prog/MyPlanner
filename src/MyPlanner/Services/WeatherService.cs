using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace MyPlanner.Services;

public class WeatherInfo
{
    public string Query { get; set; } = "";
    public string City { get; set; } = "";
    public double Temp { get; set; }
    public int Code { get; set; }
    public bool IsDay { get; set; } = true;
    public DateTime Updated { get; set; }
}

/// <summary>Погода через бесплатный сервис Open-Meteo (без ключей). Последние данные кешируются.</summary>
public sealed class WeatherService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    public WeatherInfo? Current { get; private set; }
    public bool LastRefreshFailed { get; private set; }
    public event Action? Changed;

    public WeatherService()
    {
        try
        {
            var s = Svc.Settings.WeatherCache;
            if (!string.IsNullOrEmpty(s)) Current = JsonSerializer.Deserialize<WeatherInfo>(s);
        }
        catch { Current = null; }
    }

    /// <summary>Возвращает true, если данные обновились.</summary>
    public async Task<bool> RefreshAsync()
    {
        var city = Svc.Settings.City.Trim();
        if (city.Length == 0)
        {
            Current = null;
            LastRefreshFailed = false;
            Changed?.Invoke();
            return false;
        }
        try
        {
            var lang = Loc.I.Current[..2];
            var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city)}&count=1&language={lang}&format=json";
            using var geo = JsonDocument.Parse(await Http.GetStringAsync(geoUrl));
            if (!geo.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
            {
                LastRefreshFailed = true;
                Current = new WeatherInfo { Query = city, City = city, Code = -1, Updated = DateTime.Now };
                Changed?.Invoke();
                return false;
            }
            var r = results[0];
            double lat = r.GetProperty("latitude").GetDouble();
            double lon = r.GetProperty("longitude").GetDouble();
            string name = r.TryGetProperty("name", out var n) ? n.GetString() ?? city : city;

            var url = string.Format(CultureInfo.InvariantCulture,
                "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current=temperature_2m,weather_code,is_day&timezone=auto",
                lat, lon);
            using var fc = JsonDocument.Parse(await Http.GetStringAsync(url));
            var cur = fc.RootElement.GetProperty("current");
            Current = new WeatherInfo
            {
                Query = city,
                City = name,
                Temp = cur.GetProperty("temperature_2m").GetDouble(),
                Code = cur.GetProperty("weather_code").GetInt32(),
                IsDay = cur.TryGetProperty("is_day", out var d) ? d.GetInt32() == 1 : true,
                Updated = DateTime.Now
            };
            Svc.Settings.WeatherCache = JsonSerializer.Serialize(Current);
            LastRefreshFailed = false;
            Changed?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            Logger.Write("Weather refresh failed", ex);
            LastRefreshFailed = true;
            Changed?.Invoke();
            return false;
        }
    }

    public static string Icon(int code, bool isDay) => code switch
    {
        0 => isDay ? "☀️" : "🌙",
        1 or 2 => isDay ? "🌤️" : "☁️",
        3 => "☁️",
        45 or 48 => "🌫️",
        >= 51 and <= 57 => "🌦️",
        >= 61 and <= 67 => "🌧️",
        >= 71 and <= 77 => "🌨️",
        >= 80 and <= 82 => "🌧️",
        85 or 86 => "🌨️",
        >= 95 => "⛈️",
        _ => "🌡️"
    };

    public static string DescriptionKey(int code) => code switch
    {
        0 => "wx_clear",
        1 or 2 => "wx_partly",
        3 => "wx_cloudy",
        45 or 48 => "wx_fog",
        >= 51 and <= 57 => "wx_drizzle",
        >= 61 and <= 67 => "wx_rain",
        >= 71 and <= 77 => "wx_snow",
        >= 80 and <= 82 => "wx_showers",
        85 or 86 => "wx_snow",
        >= 95 => "wx_thunder",
        _ => "wx_unknown"
    };
}
