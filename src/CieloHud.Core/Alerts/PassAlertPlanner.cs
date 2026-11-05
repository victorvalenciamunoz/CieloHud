using CieloHud.Core.Passes;

namespace CieloHud.Core.Alerts;

/// <summary>
/// Decides which visible passes to announce; <see cref="AlertPlanner"/> decides when. Pure: the caller supplies the passes,
/// the TLE epoch, the current instant, the observer's time zone and the passes already announced.
/// <list type="bullet">
/// <item>Each pass is announced <see cref="AlertSettings.LeadTime"/> before its visible start, and is worth it until it starts.</item>
/// <item>Passes too far from the TLE epoch or already announced are left out.</item>
/// </list>
/// </summary>
public sealed class PassAlertPlanner
{
    private readonly AlertPlanner _timing;

    public PassAlertPlanner(AlertSettings? settings = null)
    {
        _timing = new AlertPlanner(settings);
    }

    public AlertSettings Settings => _timing.Settings;

    /// <summary>Alerts for the given passes, ordered by <see cref="PassAlert.NotifyAt"/>. All instants in UTC.</summary>
    /// <param name="alreadyNotified">Visible starts of the passes announced before; matched with <see cref="AlertSettings.SamePassTolerance"/>.</param>
    public IReadOnlyList<PassAlert> Plan(
        IEnumerable<VisiblePass> passes,
        DateTimeOffset tleEpoch,
        DateTimeOffset now,
        TimeZoneInfo timeZone,
        IEnumerable<DateTimeOffset>? alreadyNotified = null)
    {
        ArgumentNullException.ThrowIfNull(passes);
        var notified = alreadyNotified?.ToList() ?? [];
        var lastTrusted = tleEpoch + Settings.MaxTleAge;

        var candidates = passes
            .Where(p => p.VisibleStart.Instant <= lastTrusted && !WasNotified(p.VisibleStart.Instant, notified))
            .Select(p => new AlertCandidate<VisiblePass>(p, p.VisibleStart.Instant - Settings.LeadTime, p.VisibleStart.Instant));

        return _timing.Plan(candidates, now, timeZone)
            .Select(a => new PassAlert(a.Subject, a.NotifyAt, a.IsEveningBefore))
            .ToList();
    }

    /// <summary>True when the local time of day of <paramref name="instant"/> falls in the quiet hours.</summary>
    public bool IsQuiet(DateTimeOffset instant, TimeZoneInfo timeZone) => _timing.IsQuiet(instant, timeZone);

    private bool WasNotified(DateTimeOffset visibleStart, List<DateTimeOffset> notified) =>
        notified.Any(n => (n - visibleStart).Duration() <= Settings.SamePassTolerance);
}
