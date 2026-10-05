namespace CieloHud.Core.Alerts;

/// <summary>
/// When to announce visible passes and Moon-planet conjunctions. Fixed by design (no user input): ahead of time, never at night.
/// Times of day are local to the observer's time zone.
/// </summary>
public sealed record AlertSettings
{
    /// <summary>How long before the visible start the alert goes off: time to go outside and calibrate the compass.</summary>
    public TimeSpan LeadTime { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How long before a conjunction window opens its alert goes off. It lasts hours, so this is a heads-up to plan the evening:
    /// for an evening window, about sunset.
    /// </summary>
    public TimeSpan ConjunctionLeadTime { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>Start of the quiet hours (inclusive). No alert goes off in [<see cref="QuietStart"/>, <see cref="QuietEnd"/>).</summary>
    public TimeSpan QuietStart { get; init; } = TimeSpan.Zero;

    /// <summary>End of the quiet hours (exclusive).</summary>
    public TimeSpan QuietEnd { get; init; } = TimeSpan.FromHours(7);

    /// <summary>An alert that would fall in the quiet hours goes off the evening before, at this time.</summary>
    public TimeSpan EveningReminder { get; init; } = TimeSpan.FromHours(22);

    /// <summary>
    /// Passes later than this after the TLE epoch are not announced: ISS reboosts shift the orbit, and an old TLE
    /// can be minutes off. The app refreshes the TLE daily, so this only bites after days without network.
    /// </summary>
    public TimeSpan MaxTleAge { get; init; } = TimeSpan.FromDays(4);

    /// <summary>
    /// Two visible starts closer than this are the same pass. Recomputing with a newer TLE moves a pass by seconds,
    /// and it must not be announced twice.
    /// </summary>
    public TimeSpan SamePassTolerance { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// A conjunction with a planet whose best moment is closer than this to one already announced with the same planet is the same
    /// approach (the dawn and the dusk of one day): it is not announced again.
    /// </summary>
    public TimeSpan SameConjunctionTolerance { get; init; } = TimeSpan.FromDays(1);

    /// <summary>How far ahead the app looks for passes and conjunctions on each recalculation.</summary>
    public TimeSpan Horizon { get; init; } = TimeSpan.FromDays(3);

    /// <summary>
    /// How often the alerts are planned again with the app closed: a fresher TLE, and the passes that enter the horizon.
    /// Shorter than the horizon, so a pass is always planned before its alert is due.
    /// </summary>
    public TimeSpan RefreshInterval { get; init; } = TimeSpan.FromDays(1);

    /// <summary>When planning could not run (no location yet, no TLE and no network), the next try comes sooner.</summary>
    public TimeSpan RetryInterval { get; init; } = TimeSpan.FromHours(3);

    public static AlertSettings Default { get; } = new();

    internal void Validate()
    {
        var day = TimeSpan.FromDays(1);
        if (LeadTime < TimeSpan.Zero || ConjunctionLeadTime < TimeSpan.Zero)
            throw new ArgumentException("Lead times cannot be negative.");
        // Quiet hours within one calendar day, evening reminder after them: the reminder is always the evening before.
        if (QuietStart < TimeSpan.Zero || QuietStart >= QuietEnd || QuietEnd > EveningReminder || EveningReminder >= day)
            throw new ArgumentException("Expected 0 <= QuietStart < QuietEnd <= EveningReminder < 24 h.");
        if (RetryInterval <= TimeSpan.Zero || RefreshInterval < RetryInterval || RefreshInterval >= Horizon)
            throw new ArgumentException("Expected 0 < RetryInterval <= RefreshInterval < Horizon.");
    }
}
