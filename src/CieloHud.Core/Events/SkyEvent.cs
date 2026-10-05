namespace CieloHud.Core.Events;

/// <summary>What kind of thing there is to see.</summary>
public enum SkyEventKind
{
    /// <summary>A visible ISS pass.</summary>
    Pass,

    /// <summary>The Moon next to a planet.</summary>
    MoonConjunction,

    /// <summary>Two planets together, on the closest night of their approach.</summary>
    PlanetPair,
}

/// <summary>
/// Something worth looking at, for the list of upcoming events (decision 030). Words in Spanish, times in the observer's zone,
/// written in absolute terms ("7:45"), not relative to an alert ("mañana temprano").
/// </summary>
/// <param name="At">When to look: the visible start of a pass, the best moment of a conjunction (UTC).</param>
/// <param name="Until">When it is over: the end of the pass or of the window (UTC).</param>
/// <param name="Target">The HUD target that guides to it: "ISS", "Luna", "Júpiter"…</param>
/// <param name="Time">The time to show: "7:54" for a pass, "≈7:45" for a conjunction (rounded to the quarter hour).</param>
/// <param name="Title">"Pasa la ISS", "La Luna junto a Júpiter (2°)", "Marte junto a Júpiter (1°)".</param>
/// <param name="Details">Where and how long: "11 min · aparece por el SO, máximo 37° al SE", "al E", "al S · juntos del 9 al 23 nov".</param>
/// <param name="AlertAt">When its alert goes off (UTC), with the same rules as the alerts; null if it will not get one (already announced, too late).</param>
public sealed record SkyEvent(
    SkyEventKind Kind,
    DateTimeOffset At,
    DateTimeOffset Until,
    string Target,
    string Time,
    string Title,
    string Details,
    DateTimeOffset? AlertAt);
