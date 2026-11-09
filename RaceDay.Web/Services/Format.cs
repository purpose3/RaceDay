using System.Globalization;

namespace RaceDay.Web.Services;

/// <summary>Display helpers so dates, money, times and positions look the same on every page.</summary>
public static class Format
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Ordinal(int n)
    {
        var lastTwo = n % 100;
        if (lastTwo is >= 11 and <= 13) return $"{n}th";
        return (n % 10) switch { 1 => $"{n}st", 2 => $"{n}nd", 3 => $"{n}rd", _ => $"{n}th" };
    }

    public static string Time(TimeSpan t) => t.ToString(@"hh\:mm\:ss", Inv);
    public static string Money(decimal v) => "R" + v.ToString("N2", Inv);
    public static string Date(DateTime d) => d.ToString("ddd, d MMM yyyy", Inv);
    public static string Km(decimal d) => d.ToString("0.##", Inv) + " km";
    public static string StatusClass(string status) => "status-" + status.ToLowerInvariant();

    public static string EventIcon(string type) => type switch
    {
        "Run" => "🏃",
        "Walk" => "🚶",
        "Cycle" => "🚴",
        _ => "🏁"
    };
}
