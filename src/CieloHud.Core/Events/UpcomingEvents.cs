using CieloHud.Core.Alerts;
using CieloHud.Core.Conjunctions;
using CieloHud.Core.Passes;
using static CieloHud.Core.Alerts.AlertWords;

namespace CieloHud.Core.Events;

/// <summary>
/// The list of upcoming events (decision 030): visible ISS passes and conjunctions, in order, each with when its alert will go
/// off. Pure: the caller finds the passes and conjunctions, for as far ahead as it wants to show. The alert times come from the
/// same planners as the alerts, so the list and the notifications agree.
/// </summary>
public static class UpcomingEvents
{
    /// <summary>Events ordered by when to look, from the passes and conjunctions found.</summary>
    /// <param name="tleEpoch">Epoch of the TLE the passes were found with; null when there are no passes (no orbit).</param>
    public static IReadOnlyList<SkyEvent> Build(
        IEnumerable<VisiblePass> passes,
        DateTimeOffset? tleEpoch,
        IEnumerable<Conjunction> conjunctions,
        DateTimeOffset now,
        TimeZoneInfo timeZone,
        IEnumerable<DateTimeOffset>? notifiedPasses = null,
        IEnumerable<NotifiedConjunction>? notifiedConjunctions = null,
        AlertSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(passes);
        ArgumentNullException.ThrowIfNull(conjunctions);
        ArgumentNullException.ThrowIfNull(timeZone);
        var passList = passes.Where(p => p.VisibleEnd.Instant > now).ToList();
        var conjunctionList = conjunctions.Where(c => c.End.Instant > now).ToList();

        var passAlerts = tleEpoch is { } epoch
            ? new PassAlertPlanner(settings).Plan(passList, epoch, now, timeZone, notifiedPasses)
            : [];
        var conjunctionAlerts = new ConjunctionAlertPlanner(settings).Plan(conjunctionList, now, timeZone, notifiedConjunctions);

        var events = passList.Select(p => FromPass(p, passAlerts.FirstOrDefault(a => ReferenceEquals(a.Pass, p))?.NotifyAt, timeZone))
            .Concat(conjunctionList.Select(c => FromConjunction(c,
                conjunctionAlerts.FirstOrDefault(a => a.Conjunctions.Any(x => ReferenceEquals(x, c)))?.NotifyAt, timeZone)));
        return events.OrderBy(e => e.At).ToList();
    }

    /// <summary>The heading for an event's day: "Hoy", "Mañana", "mié 8 oct".</summary>
    public static string DayLabel(DateTimeOffset at, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var day = TimeZoneInfo.ConvertTime(at, timeZone);
        return (day.Date - TimeZoneInfo.ConvertTime(now, timeZone).Date).Days switch
        {
            0 => "Hoy",
            1 => "Mañana",
            _ => $"{Weekdays[(int)day.DayOfWeek]} {day.Day.ToString(Culture)} {Months[day.Month - 1]}",
        };
    }

    /// <summary>When its alert goes off, in words: "Aviso hoy a las 22:00", "Aviso el dom 15 nov a las 22:00".</summary>
    public static string AlertLabel(DateTimeOffset alertAt, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var local = TimeZoneInfo.ConvertTime(alertAt, timeZone);
        var day = DayLabel(alertAt, now, timeZone);
        var when = day is "Hoy" or "Mañana" ? day.ToLowerInvariant() : $"el {day}";
        return $"Aviso {when} a {TheTime(local)}";
    }

    private static SkyEvent FromPass(VisiblePass pass, DateTimeOffset? alertAt, TimeZoneInfo timeZone) => new(
        SkyEventKind.Pass,
        pass.VisibleStart.Instant,
        pass.VisibleEnd.Instant,
        "ISS",
        Time(TimeZoneInfo.ConvertTime(pass.VisibleStart.Instant, timeZone)),
        "Pasa la ISS",
        $"{PassAlertText.Minutes(pass)} min · {PassAlertText.Where(pass)}",
        alertAt);

    private static SkyEvent FromConjunction(Conjunction c, DateTimeOffset? alertAt, TimeZoneInfo timeZone)
    {
        var best = TimeZoneInfo.ConvertTime(c.Best.Instant, timeZone);
        var separation = ConjunctionAlertText.Separation(c.Best.SeparationDegrees);
        var title = c.IsWithMoon
            ? $"La Luna junto a {ConjunctionAlertText.PlanetName(c.Companion)} ({separation})"
            : $"{ConjunctionAlertText.PlanetName(c.Companion)} junto a {ConjunctionAlertText.PlanetName(c.Guide)} ({separation})";
        var details = $"al {Cardinal(c.Best.Guide.CardinalPoint)}"
            + (ConjunctionAlertText.NightsTogether(c, timeZone) is { } nights ? $" · juntos {nights}" : "");
        return new SkyEvent(
            c.IsWithMoon ? SkyEventKind.MoonConjunction : SkyEventKind.PlanetPair,
            c.Best.Instant,
            c.End.Instant,
            ConjunctionAlertText.GuideName(c.Guide),
            "≈" + Time(ConjunctionAlertText.Approximate(c, best, timeZone)),
            title,
            details,
            alertAt);
    }
}
