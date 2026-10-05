using CieloHud.Core.Conjunctions;
using CieloHud.Core.SolarSystem;
using static CieloHud.Core.Alerts.AlertWords;

namespace CieloHud.Core.Alerts;

/// <summary>
/// The words of a conjunction alert, in Spanish (decision 026):
/// "Esta noche, la Luna junto a Júpiter (3°) · mejor hacia las 22:00 al SE".
/// The time is rounded to the quarter hour (it lasts hours; "hacia" says it is approximate) and the direction is the Moon's,
/// which the HUD guides to when the alert is tapped.
/// </summary>
public static class ConjunctionAlertText
{
    /// <summary>Quarter hours: the separation changes by about 0.1° in that time.</summary>
    private const int RoundingMinutes = 15;

    /// <summary>Headline: "La Luna junto a Júpiter", "La Luna junto a Júpiter y Marte".</summary>
    public static string Title(ConjunctionAlert alert) =>
        $"La Luna junto a {JoinNames(alert.Conjunctions.Select(c => PlanetName(c.Planet)))}";

    /// <summary>When, how close and where to look.</summary>
    public static string Body(ConjunctionAlert alert, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var notify = TimeZoneInfo.ConvertTime(alert.NotifyAt, timeZone);
        var planets = JoinNames(alert.Conjunctions.Select(c => $"{PlanetName(c.Planet)} ({Separation(c.Best.SeparationDegrees)})"));
        var best = alert.Closest.Best;

        // Opened late, past the best moment: say where it is now-ish and until when.
        if (best.Instant < alert.NotifyAt)
        {
            var end = alert.Closest.End;
            return $"Ahora, la Luna junto a {planets} · al {Cardinal(end.Moon.CardinalPoint)}, hasta {TheTime(Local(alert.WindowEnd, timeZone))}";
        }

        var bestLocal = TimeZoneInfo.ConvertTime(best.Instant, timeZone);
        return $"{When(alert, notify, bestLocal)}, la Luna junto a {planets} · " +
            $"mejor hacia {TheTime(Approximate(alert, bestLocal, timeZone))} al {Cardinal(best.Moon.CardinalPoint)}";
    }

    /// <summary>The planet's name in Spanish.</summary>
    public static string PlanetName(CelestialBody planet) => planet switch
    {
        CelestialBody.Mercury => "Mercurio",
        CelestialBody.Venus => "Venus",
        CelestialBody.Mars => "Marte",
        CelestialBody.Jupiter => "Júpiter",
        CelestialBody.Saturn => "Saturno",
        _ => throw new ArgumentOutOfRangeException(nameof(planet), planet, "Not a planet."),
    };

    private static string When(ConjunctionAlert alert, DateTimeOffset notify, DateTimeOffset best)
    {
        if (alert.NotifyAt >= alert.WindowStart)
            return "Ahora";
        return ((best.Date - notify.Date).Days, best.Hour) switch
        {
            (0, >= 12) => "Esta noche",
            (0, _) => "Esta mañana",
            // Said at 22:00 the evening before: "esta madrugada" for the small hours, "mañana temprano" for dawn after 7.
            (1, < 7) => "Esta madrugada",
            (1, < 12) => "Mañana temprano",
            (1, _) => "Mañana por la noche",
            _ => $"El día {best.Day.ToString(Culture)}",
        };
    }

    // Whole degrees; under half a degree is still "less than one", not "0°".
    private static string Separation(double degrees) =>
        Round(degrees) == 0 ? "menos de 1°" : $"{Degrees(degrees)}°";

    private static string JoinNames(IEnumerable<string> names)
    {
        var list = names.ToList();
        return list.Count == 1 ? list[0] : $"{string.Join(", ", list[..^1])} y {list[^1]}";
    }

    private static DateTimeOffset Local(DateTimeOffset instant, TimeZoneInfo timeZone) => TimeZoneInfo.ConvertTime(instant, timeZone);

    // To the quarter hour, unless that falls outside a short window (23:20-23:45 must not say "hacia las 23:15").
    private static DateTimeOffset Approximate(ConjunctionAlert alert, DateTimeOffset bestLocal, TimeZoneInfo timeZone)
    {
        var rounded = RoundToQuarter(bestLocal, timeZone);
        return rounded >= alert.Closest.Start.Instant && rounded <= alert.Closest.End.Instant ? rounded : bestLocal;
    }

    private static DateTimeOffset RoundToQuarter(DateTimeOffset local, TimeZoneInfo timeZone)
    {
        var minutes = local.TimeOfDay.TotalMinutes;
        var rounded = Round(minutes / RoundingMinutes) * RoundingMinutes;
        return Local(local.AddMinutes(rounded - minutes), timeZone);
    }
}
