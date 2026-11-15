using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace PitzkiWidgets.Core;

/// <summary>
/// The weather outside, from open-meteo: no key, no account, and the only thing that leaves this PC
/// is the name of the city on the card. The place is looked up once and then kept in the board.
/// </summary>
public sealed class Sky
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    DateTime _last;
    bool _busy;

    /// <summary>"18° Fair", or "" while nothing has come back yet.</summary>
    public string Text { get; private set; } = "";
    public double Temp { get; private set; }
    public int Code { get; private set; } = -1;
    public bool Day { get; private set; } = true;
    public string Trouble { get; private set; } = "";

    public event Action? Changed;

    /// <summary>
    /// Guesses the user's city from their internet connection, using GeoJS (free, no account). It is
    /// accurate to the city, which is all a forecast needs, and only runs when the user presses the
    /// button. Null when it cannot tell: offline, or a connection that maps to no place.
    /// </summary>
    public static async Task<(string City, double Lat, double Lon)?> LocateAsync()
    {
        try
        {
            string json = await Http.GetStringAsync("https://get.geojs.io/v1/ip/geo.json");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string Text(string key) => root.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

            // GeoJS sends the coordinates as text
            if (!double.TryParse(Text("latitude"), NumberStyles.Float, Inv, out double lat) ||
                !double.TryParse(Text("longitude"), NumberStyles.Float, Inv, out double lon)) return null;

            string city = Text("city");
            if (city.Length == 0) city = Text("region");      // some connections only resolve to a region
            return city.Length == 0 ? null : (city, lat, lon);
        }
        catch { return null; }
    }

    /// <summary>Asks again at most every ten minutes, so the card shows the weather now.</summary>
    public async Task RefreshAsync(Board board, bool force = false)
    {
        string city = board.WeatherCity.Trim();
        if (city.Length == 0)
        {
            Text = ""; Code = -1;
            Trouble = "Pick a city in the Widgets window";
            Changed?.Invoke();
            return;
        }
        if (_busy || (!force && DateTime.Now - _last < TimeSpan.FromMinutes(10))) return;
        _busy = true;
        _last = DateTime.Now;

        try
        {
            if (!string.Equals(board.WeatherPlace, city, StringComparison.OrdinalIgnoreCase))
            {
                string found = await Http.GetStringAsync(
                    "https://geocoding-api.open-meteo.com/v1/search?count=1&name=" + Uri.EscapeDataString(city));
                using var where = JsonDocument.Parse(found);
                if (!where.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                {
                    Text = ""; Code = -1;
                    Trouble = $"Couldn't find \"{city}\".";
                    return;
                }
                board.WeatherLat = results[0].GetProperty("latitude").GetDouble();
                board.WeatherLon = results[0].GetProperty("longitude").GetDouble();
                board.WeatherPlace = city;
                board.Save();
            }

            string json = await Http.GetStringAsync(
                "https://api.open-meteo.com/v1/forecast?current=temperature_2m,weather_code,is_day" +
                "&latitude=" + board.WeatherLat.ToString("0.###", Inv) +
                "&longitude=" + board.WeatherLon.ToString("0.###", Inv));
            using var doc = JsonDocument.Parse(json);
            var now = doc.RootElement.GetProperty("current");
            Temp = now.GetProperty("temperature_2m").GetDouble();
            Code = now.GetProperty("weather_code").GetInt32();
            Day = !now.TryGetProperty("is_day", out var light) || light.GetInt32() != 0;
            Text = $"{Math.Round(Temp)}° {Describe(Code)}";
            Trouble = "";
        }
        catch
        {
            Trouble = "Couldn't reach the weather";
            _last = DateTime.Now - TimeSpan.FromMinutes(7);   // try again in about three minutes
        }
        finally
        {
            _busy = false;
            Changed?.Invoke();
        }
    }

    /// <summary>WMO weather codes, one word each.</summary>
    public static string Describe(int code) => code switch
    {
        0 => "Clear",
        1 or 2 => "Fair",
        3 => "Cloudy",
        45 or 48 => "Fog",
        51 or 53 or 55 or 56 or 57 => "Drizzle",
        61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "Rain",
        71 or 73 or 75 or 77 or 85 or 86 => "Snow",
        95 or 96 or 99 => "Storm",
        _ => "",
    };

    /// <summary>The one picture that goes with it: sun, moon, cloud, rain, snow, storm or fog.</summary>
    public static string Kind(int code, bool day) => code switch
    {
        < 0 => "",
        0 or 1 => day ? "sun" : "moon",
        2 or 3 => "cloud",
        45 or 48 => "fog",
        >= 71 and <= 77 or 85 or 86 => "snow",
        >= 95 => "storm",
        _ => "rain",
    };
}
