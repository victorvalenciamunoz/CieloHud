using CieloHud.Core.Conjunctions;
using CieloHud.Core.SolarSystem;
using static CieloHud.Core.Alerts.AlertWords;

namespace CieloHud.Core.Alerts;

/// <summary>
/// The words of a conjunction alert, in Spanish (decisions 026 and 029):
/// "Esta noche, la Luna junto a Júpiter (3°) · mejor hacia las 22:00 al SE";
/// "Mañana temprano, Marte junto a Júpiter (1°), lo más cerca en estas semanas · mejor hacia las 7:30 al S · juntos del 9 al 23 nov".
/// The time is rounded to the quarter hour (it lasts hours; "hacia" says it is approximate) and the direction is the guide's
/// (the Moon or the brighter planet), which the HUD leads to when the alert is tapped.
/// </summary>
public static class ConjunctionAlertText
{
    /// <summary>Quarter hours: the separation changes by about 0.1° in that time.</summary>
    private const int RoundingMinutes = 15;

    /// <summary>Headline: "La Luna junto a Júpiter", "La Luna junto a Júpiter y Marte", "Marte junto a Júpiter".</summary>
    public static string Title(ConjunctionAlert alert) => alert.Closest.IsWithMoon
        ? $"La Luna junto a {JoinNames(alert.Conjunctions.Select(c => PlanetName(c.Companion)))}"
        : $"{PlanetName(alert.Closest.Companion)} junto a {PlanetName(alert.Closest.Guide)}";

    /// <summary>When, how close and where to look; for two planets, also the nights they are together.</summary>
    public static string Body(ConjunctionAlert alert, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var notify = TimeZoneInfo.ConvertTime(alert.NotifyAt, timeZone);
        var closest = alert.Closest;
        var what = closest.IsWithMoon
            ? "la Luna junto a " + JoinNames(alert.Conjunctions.Select(c => $"{PlanetName(c.Companion)} ({Separation(c.Best.SeparationDegrees)})"))
            : $"{PlanetName(closest.Companion)} junto a {PlanetName(closest.Guide)} ({Separation(closest.Best.SeparationDegrees)}), lo más cerca en estas semanas";
        var nights = NightsTogether(closest, timeZone) is { } range ? $" · juntos {range}" : "";
        var best = closest.Best;

        // Opened late, past the best moment: say where it is now-ish and until when.
        if (best.Instant < alert.NotifyAt)
            return $"Ahora, {what} · al {Cardinal(closest.End.Guide.CardinalPoint)}, hasta {TheTime(Local(alert.WindowEnd, timeZone))}{nights}";

        var bestLocal = TimeZoneInfo.ConvertTime(best.Instant, timeZone);
        return $"{When(alert, notify, bestLocal)}, {what} · " +
            $"mejor hacia {TheTime(Approximate(closest, bestLocal, timeZone))} al {Cardinal(best.Guide.CardinalPoint)}{nights}";
    }

    /// <summary>What the HUD is called for the guide body: "Luna", "Júpiter"…</summary>
    public static string GuideName(CelestialBody guide) => guide == CelestialBody.Moon ? "Luna" : PlanetName(guide);


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

    // "del 9 al 23 nov", "del 28 oct al 5 nov": the local dates the windows start on. Null for the Moon, or a single night.
    internal static string? NightsTogether(Conjunction conjunction, TimeZoneInfo timeZone)
    {
        return conjunction.Nights is { } nights ? DateRange(Local(nights.First, timeZone), Local(nights.Last, timeZone)) : null;
    }

    // Whole degrees; under half a degree is still "less than one", not "0°".
    internal static string Separation(double degrees) =>
        Round(degrees) == 0 ? "menos de 1°" : $"{Degrees(degrees)}°";

    private static string JoinNames(IEnumerable<string> names)
    {
        var list = names.ToList();
        return list.Count == 1 ? list[0] : $"{string.Join(", ", list[..^1])} y {list[^1]}";
    }

    private static DateTimeOffset Local(DateTimeOffset instant, TimeZoneInfo timeZone) => TimeZoneInfo.ConvertTime(instant, timeZone);

    // To the quarter hour, unless that falls outside a short window (23:20-23:45 must not say "hacia las 23:15").
    internal static DateTimeOffset Approximate(Conjunction conjunction, DateTimeOffset bestLocal, TimeZoneInfo timeZone)
    {
        var rounded = RoundToQuarter(bestLocal, timeZone);
        return rounded >= conjunction.Start.Instant && rounded <= conjunction.End.Instant ? rounded : bestLocal;
    }

    private static DateTimeOffset RoundToQuarter(DateTimeOffset local, TimeZoneInfo timeZone)
    {
        var minutes = local.TimeOfDay.TotalMinutes;
        var rounded = Round(minutes / RoundingMinutes) * RoundingMinutes;
        return Local(local.AddMinutes(rounded - minutes), timeZone);
    }
}
