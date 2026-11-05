using CieloHud.Core.Apparitions;

namespace CieloHud.Core.Alerts;

/// <summary>
/// Decides which seasons of Mercury to announce; <see cref="AlertPlanner"/> decides when, with the same rules as the passes and
/// conjunctions (decision 034). Pure: the caller supplies the seasons, the current instant, the observer's time zone and those already announced.
/// <list type="bullet">
/// <item>One alert per season, on its best day: <see cref="AlertSettings.MercuryLeadTime"/> before that day's window opens
/// (at dawn, the evening before), worth it until the window closes. The text tells the other days, in case that one is cloudy.</item>
/// <item>Seasons already announced are left out: the same end of the night within <see cref="AlertSettings.SameMercurySeasonTolerance"/>.</item>
/// </list>
/// </summary>
public sealed class MercuryAlertPlanner
{
    private readonly AlertPlanner _timing;

    public MercuryAlertPlanner(AlertSettings? settings = null)
    {
        _timing = new AlertPlanner(settings);
    }

    public AlertSettings Settings => _timing.Settings;

    /// <summary>Alerts for the given seasons, ordered by <see cref="MercuryAlert.NotifyAt"/>. All instants in UTC.</summary>
    public IReadOnlyList<MercuryAlert> Plan(
        IEnumerable<MercuryApparition> apparitions,
        DateTimeOffset now,
        TimeZoneInfo timeZone,
        IEnumerable<NotifiedMercury>? alreadyNotified = null)
    {
        ArgumentNullException.ThrowIfNull(apparitions);
        var notified = alreadyNotified?.ToList() ?? [];

        var candidates = apparitions
            .Where(a => !notified.Any(n => n.Period == a.Period
                && (n.Best - a.Best.Best.Instant).Duration() < Settings.SameMercurySeasonTolerance))
            .Select(a => new AlertCandidate<MercuryApparition>(a, a.Best.Start.Instant - Settings.MercuryLeadTime, a.Best.End.Instant));

        return _timing.Plan(candidates, now, timeZone)
            .Select(a => new MercuryAlert(a.Subject, a.NotifyAt, a.IsEveningBefore))
            .ToList();
    }
}
