using System.Globalization;
using CieloHud.Core.Sky;

namespace CieloHud.Core.Alerts;

/// <summary>Spanish words and number formats shared by the alert texts.</summary>
internal static class AlertWords
{
    public static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    // Halves up, as people round (2.5 min is "3 min"), not to even.
    public static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    public static string Degrees(double degrees) => Round(degrees).ToString("F0", Culture);

    public static string Time(DateTimeOffset local) => local.ToString("H:mm", Culture);

    /// <summary>The time with its article: "la 1:05" (one o'clock is singular), "las 21:43".</summary>
    public static string TheTime(DateTimeOffset local) => (local.Hour == 1 ? "la " : "las ") + Time(local);

    public static string Cardinal(CardinalPoint point) => point switch
    {
        CardinalPoint.N => "N",
        CardinalPoint.NE => "NE",
        CardinalPoint.E => "E",
        CardinalPoint.SE => "SE",
        CardinalPoint.S => "S",
        CardinalPoint.SW => "SO",
        CardinalPoint.W => "O",
        CardinalPoint.NW => "NO",
        _ => throw new ArgumentOutOfRangeException(nameof(point), point, null),
    };
}
