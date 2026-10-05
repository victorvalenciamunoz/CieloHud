namespace CieloHud.Core.Alerts;

/// <summary>Something worth an alert: when it would like to go off and until when it is still worth it.</summary>
/// <param name="DesiredAt">The ideal alert time: the start of what is to be seen, minus its lead time.</param>
/// <param name="WorthUntil">Past this instant the alert is pointless (the pass has started, the conjunction is over).</param>
public readonly record struct AlertCandidate<T>(T Subject, DateTimeOffset DesiredAt, DateTimeOffset WorthUntil);

/// <summary>A candidate and when its alert goes off.</summary>
/// <param name="NotifyAt">When the alert goes off (UTC). Equal to the planning instant when it is already due.</param>
/// <param name="IsEveningBefore">True when it would go off in the quiet hours and was moved to the evening before.</param>
public readonly record struct PlannedAlert<T>(T Subject, DateTimeOffset NotifyAt, bool IsEveningBefore);

/// <summary>
/// When alerts go off, whatever they announce (ISS passes, conjunctions). Pure; times of day in the observer's zone.
/// <list type="bullet">
/// <item>Each alert goes off at its desired time.</item>
/// <item>One that would go off in the quiet hours goes off at <see cref="AlertSettings.EveningReminder"/> the evening before.</item>
/// <item>One whose time has passed goes off now, as long as it is still worth it and it is not the quiet hours.</item>
/// </list>
/// What deserves an alert, and which were already announced, is up to the caller.
/// </summary>
public sealed class AlertPlanner
{
    public AlertPlanner(AlertSettings? settings = null)
    {
        Settings = settings ?? AlertSettings.Default;
        Settings.Validate();
    }

    public AlertSettings Settings { get; }

    /// <summary>Alerts ordered by <see cref="PlannedAlert{T}.NotifyAt"/>, then by how soon they stop being worth it. UTC.</summary>
    public IReadOnlyList<PlannedAlert<T>> Plan<T>(IEnumerable<AlertCandidate<T>> candidates, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(timeZone);
        var utcNow = now.ToUniversalTime();

        var alerts = new List<(PlannedAlert<T> Alert, DateTimeOffset WorthUntil)>();
        foreach (var candidate in candidates)
        {
            if (candidate.WorthUntil <= utcNow)
                continue;

            var notifyAt = candidate.DesiredAt.ToUniversalTime();
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

            alerts.Add((new PlannedAlert<T>(candidate.Subject, notifyAt, eveningBefore), candidate.WorthUntil));
        }

        return alerts.OrderBy(a => a.Alert.NotifyAt).ThenBy(a => a.WorthUntil).Select(a => a.Alert).ToList();
    }

    /// <summary>True when the local time of day of <paramref name="instant"/> falls in the quiet hours.</summary>
    public bool IsQuiet(DateTimeOffset instant, TimeZoneInfo timeZone)
    {
        var timeOfDay = TimeZoneInfo.ConvertTime(instant, timeZone).TimeOfDay;
        return timeOfDay >= Settings.QuietStart && timeOfDay < Settings.QuietEnd;
    }

    // The quiet hours sit within one calendar day, so "the evening before" is the previous local date at the reminder time.
    private DateTimeOffset EveningBefore(DateTimeOffset quietInstant, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTime(quietInstant, timeZone);
        var evening = local.Date.AddDays(-1) + Settings.EveningReminder;
        return new DateTimeOffset(evening, timeZone.GetUtcOffset(evening)).ToUniversalTime();
    }
}
