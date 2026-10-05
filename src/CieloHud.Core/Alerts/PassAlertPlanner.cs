using CieloHud.Core.Passes;

namespace CieloHud.Core.Alerts;

/// <summary>
/// Decides which visible passes to announce and when. Pure: the caller supplies the passes, the TLE epoch, the current
/// instant, the observer's time zone and the passes already announced; scheduling and showing the alert are up to the app.
/// <list type="bullet">
/// <item>Each pass is announced <see cref="AlertSettings.LeadTime"/> before its visible start.</item>
/// <item>An alert that would go off in the quiet hours goes off at <see cref="AlertSettings.EveningReminder"/> the evening before.</item>
/// <item>An alert whose time has passed goes off now, as long as the pass has not started and it is not the quiet hours.</item>
/// <item>Passes too far from the TLE epoch, already started or already announced are left out.</item>
/// </list>
/// </summary>
public sealed class PassAlertPlanner
{
    public PassAlertPlanner(AlertSettings? settings = null)
    {
        Settings = settings ?? AlertSettings.Default;
        Settings.Validate();
    }

    public AlertSettings Settings { get; }

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
        ArgumentNullException.ThrowIfNull(timeZone);
        var notified = alreadyNotified?.ToList() ?? [];
        var utcNow = now.ToUniversalTime();
        var lastTrusted = tleEpoch + Settings.MaxTleAge;

        var alerts = new List<PassAlert>();
        foreach (var pass in passes)
        {
            var start = pass.VisibleStart.Instant;
            if (start <= utcNow || start > lastTrusted || WasNotified(start, notified))
                continue;

            var notifyAt = (start - Settings.LeadTime).ToUniversalTime();
            var eveningBefore = IsQuiet(notifyAt, timeZone);
            if (eveningBefore)
                notifyAt = EveningBefore(notifyAt, timeZone);

            if (notifyAt < utcNow)
            {
                // Late (the app was opened or recalculated after the alert time): now, unless it would wake someone up.
                if (IsQuiet(utcNow, timeZone))
                    continue;
                notifyAt = utcNow;
            }

            alerts.Add(new PassAlert(pass, notifyAt, eveningBefore));
        }

        return alerts.OrderBy(a => a.NotifyAt).ThenBy(a => a.Pass.VisibleStart.Instant).ToList();
    }

    /// <summary>True when the local time of day of <paramref name="instant"/> falls in the quiet hours.</summary>
    public bool IsQuiet(DateTimeOffset instant, TimeZoneInfo timeZone)
    {
        var timeOfDay = TimeZoneInfo.ConvertTime(instant, timeZone).TimeOfDay;
        return timeOfDay >= Settings.QuietStart && timeOfDay < Settings.QuietEnd;
    }

    private bool WasNotified(DateTimeOffset visibleStart, List<DateTimeOffset> notified) =>
        notified.Any(n => (n - visibleStart).Duration() <= Settings.SamePassTolerance);

    // The quiet hours sit within one calendar day, so "the evening before" is the previous local date at the reminder time.
    private DateTimeOffset EveningBefore(DateTimeOffset quietInstant, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTime(quietInstant, timeZone);
        var evening = local.Date.AddDays(-1) + Settings.EveningReminder;
        return new DateTimeOffset(evening, timeZone.GetUtcOffset(evening)).ToUniversalTime();
    }
}
