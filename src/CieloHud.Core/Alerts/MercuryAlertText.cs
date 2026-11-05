using CieloHud.Core.Apparitions;
using static CieloHud.Core.Alerts.AlertWords;

namespace CieloHud.Core.Alerts;

/// <summary>
/// The words of a Mercury alert, in Spanish (decision 034):
/// "Mañana al amanecer, Mercurio a 12°, lo más alto en estas semanas · mejor hacia las 7:35 al SE · se ve del 14 al 28 nov".
/// The altitude matters because it is low: a clear horizon is needed. The time is rounded to 5 minutes (the windows last
/// minutes, not hours) and stays within the window.
/// </summary>
public static class MercuryAlertText
{
    private const int RoundingMinutes = 5;

    /// <summary>What the HUD calls the target the alert leads to.</summary>
    public const string GuideName = "Mercurio";

    /// <summary>Headline: "Mercurio al amanecer", "Mercurio al anochecer".</summary>
    public static string Title(MercuryAlert alert) => Title(alert.Apparition.Period);

    /// <summary>"Mercurio al amanecer", "Mercurio al anochecer".</summary>
    public static string Title(TwilightPeriod period) => $"Mercurio {AtPeriod(period)}";

    /// <summary>When, how high and where to look on the best day, and the other days of the season.</summary>
    public static string Body(MercuryAlert alert, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var window = alert.Window;
        var days = Days(alert.Apparition, timeZone) is { } range ? $" · se ve {range}" : "";

        // Opened late, past the best moment (at dusk, the window's start): where it is as it sets, and until when.
        if (window.Best.Instant < alert.NotifyAt)
            return $"Ahora, Mercurio bajo al {Cardinal(window.End.Mercury.CardinalPoint)}, hasta {TheTime(Local(window.End.Instant, timeZone))}{days}";

        return $"{When(alert, timeZone)}, Mercurio a {Degrees(window.Best.Mercury.AltitudeDegrees)}°, lo más alto en estas semanas · " +
            $"mejor hacia {TheTime(Approximate(window, timeZone))} al {Cardinal(window.Best.Mercury.CardinalPoint)}{days}";
    }

    /// <summary>"del 14 al 28 nov": the local days of the season; null for a single day.</summary>
    public static string? Days(MercuryApparition apparition, TimeZoneInfo timeZone) =>
        DateRange(Local(apparition.First.Best.Instant, timeZone), Local(apparition.Last.Best.Instant, timeZone));

    /// <summary>
    /// The best moment to the nearest 5 minutes within the window. At dusk the best moment is the window's start, so 22:06 says
    /// "hacia las 22:10", not 22:05, before the sky is dark enough. A window with no such mark (19:06-19:09) keeps the exact minute.
    /// </summary>
    public static DateTimeOffset Approximate(MercuryWindow window, TimeZoneInfo timeZone)
    {
        var best = Local(window.Best.Instant, timeZone);
        var minutes = best.TimeOfDay.TotalMinutes;
        var down = minutes - Math.Floor(minutes / RoundingMinutes) * RoundingMinutes;
        var marks = new[] { best.AddMinutes(-down), best.AddMinutes(down == 0 ? 0 : RoundingMinutes - down) };
        var inside = marks.Where(m => m >= window.Start.Instant && m <= window.End.Instant)
            .OrderBy(m => (m - best).Duration())
            .ToList();
        return inside.Count > 0 ? Local(inside[0], timeZone) : best;
    }

    private static string When(MercuryAlert alert, TimeZoneInfo timeZone)
    {
        if (alert.NotifyAt >= alert.WindowStart)
            return "Ahora";
        var best = Local(alert.Window.Best.Instant, timeZone);
        var period = AtPeriod(alert.Apparition.Period);
        return (best.Date - Local(alert.NotifyAt, timeZone).Date).Days switch
        {
            0 => $"Hoy {period}",
            1 => $"Mañana {period}",
            _ => $"El {best.Day.ToString(Culture)} {Months[best.Month - 1]} {period}",
        };
    }

    private static string AtPeriod(TwilightPeriod period) => period == TwilightPeriod.Dusk ? "al anochecer" : "al amanecer";

    private static DateTimeOffset Local(DateTimeOffset instant, TimeZoneInfo timeZone) => TimeZoneInfo.ConvertTime(instant, timeZone);
}
