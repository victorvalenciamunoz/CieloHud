namespace CieloHud.Core.Alerts;

/// <summary>
/// Reaching an alert time with alarms the system may deliver late (decision 023). Measured on ColorOS (Android 16): an
/// alarm requested exactly, even with <c>SCHEDULE_EXACT_ALARM</c>, gets a delivery window of
/// <see cref="WindowFraction"/> of its delay, at most <see cref="MaxWindow"/>, and goes off at the end of it.
/// <para>
/// So the alarm is armed early enough that even the end of its window falls on the target; if it goes off before,
/// it is armed again for what is left. Where alarms are exact, the steps converge on the target in a few wake-ups.
/// </para>
/// </summary>
public static class AlarmSteps
{
    /// <summary>Delivery window as a fraction of the delay (Android's heuristic for inexact alarms).</summary>
    public const double WindowFraction = 0.75;

    /// <summary>The window never grows past this.</summary>
    public static readonly TimeSpan MaxWindow = TimeSpan.FromHours(1);

    /// <summary>A wake-up this close to the target posts the alert: one more step would not be worth it.</summary>
    public static readonly TimeSpan DueMargin = TimeSpan.FromSeconds(30);

    /// <summary>True when an alarm going off at <paramref name="now"/> should post the alert for <paramref name="target"/>.</summary>
    public static bool IsDue(DateTimeOffset now, DateTimeOffset target) => target - now <= DueMargin;

    /// <summary>
    /// When to ask for the next alarm so that, if the system delivers it at the end of its window, it goes off at
    /// <paramref name="target"/>: the latest T with T + window(T) ≤ target, where window(T) = min(fraction × (T − now), max).
    /// </summary>
    public static DateTimeOffset NextWakeUp(DateTimeOffset now, DateTimeOffset target)
    {
        var remaining = target - now;
        if (remaining <= TimeSpan.Zero)
            return now;

        // The window reaches its cap when the delay is at least max / fraction; then arming max before the target is enough.
        var capReachedAt = MaxWindow / WindowFraction;
        var delay = remaining - MaxWindow >= capReachedAt
            ? remaining - MaxWindow
            : remaining / (1 + WindowFraction);
        return now + delay;
    }
}
