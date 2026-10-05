namespace CieloHud.Core.Alerts;

/// <summary>
/// When to announce a visible pass. Fixed by design (no user input): ten minutes ahead, never at night.
/// Times of day are local to the observer's time zone.
/// </summary>
public sealed record AlertSettings
{
    /// <summary>How long before the visible start the alert goes off: time to go outside and calibrate the compass.</summary>
    public TimeSpan LeadTime { get; init; } = TimeSpan.FromMinutes(10);

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

    /// <summary>How far ahead the app looks for passes on each recalculation.</summary>
    public TimeSpan Horizon { get; init; } = TimeSpan.FromDays(3);

    public static AlertSettings Default { get; } = new();

    internal void Validate()
    {
        var day = TimeSpan.FromDays(1);
        if (LeadTime < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(LeadTime), LeadTime, "Lead time cannot be negative.");
        // Quiet hours within one calendar day, evening reminder after them: the reminder is always the evening before.
        if (QuietStart < TimeSpan.Zero || QuietStart >= QuietEnd || QuietEnd > EveningReminder || EveningReminder >= day)
            throw new ArgumentException("Expected 0 <= QuietStart < QuietEnd <= EveningReminder < 24 h.");
    }
}
