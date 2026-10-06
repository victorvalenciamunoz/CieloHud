namespace CieloHud.Core.Cards;

/// <summary>The four instants calendars mark, when the Moon–Sun longitude difference is 0°, 90°, 180° or 270°.</summary>
public enum MoonQuarterKind
{
    NewMoon,
    FirstQuarter,
    FullMoon,
    LastQuarter,
}

/// <summary>A principal phase and when it happens (UTC).</summary>
public readonly record struct MoonQuarter(MoonQuarterKind Kind, DateTimeOffset Instant);

/// <summary>What the Moon looks like on a given day: one of the four principal phases on its day, or the stretch between them.</summary>
public enum MoonPhaseName
{
    NewMoon,
    WaxingCrescent,
    FirstQuarter,
    WaxingGibbous,
    FullMoon,
    WaningGibbous,
    LastQuarter,
    WaningCrescent,
}

/// <summary>The Moon right now, for its card.</summary>
/// <param name="DistanceKm">From the observer to the Moon's center.</param>
/// <param name="IlluminatedFraction">Lit part of the disc as seen from the observer, 0 to 1.</param>
/// <param name="PhaseDegrees">Moon–Sun geocentric ecliptic longitude difference, 0 to 360: the conventional phase.</param>
/// <param name="Next">The next principal phase.</param>
public sealed record MoonFacts(
    double DistanceKm,
    TimeSpan LightTime,
    double IlluminatedFraction,
    double PhaseDegrees,
    MoonPhaseName Phase,
    MoonQuarter Next);
