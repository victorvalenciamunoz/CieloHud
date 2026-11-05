using CieloHud.Core.Sky;

namespace CieloHud.Core.Apparitions;

/// <summary>Mercury is seen either after sunset or before sunrise, never both in the same season.</summary>
public enum TwilightPeriod
{
    /// <summary>After sunset, low in the west.</summary>
    Dusk,

    /// <summary>Before sunrise, low in the east.</summary>
    Dawn,
}

/// <summary>Where Mercury is at one instant, and how dark the sky is.</summary>
/// <param name="Mercury">Apparent (refracted) position: what the eye sees.</param>
/// <param name="SunAltitudeDegrees">Geometric altitude of the Sun's center.</param>
public readonly record struct MercuryPoint(DateTimeOffset Instant, HorizontalPosition Mercury, double SunAltitudeDegrees);

/// <summary>
/// One twilight when Mercury can be seen: the minutes when it meets <see cref="MercuryCriteria"/>, and the best moment to look.
/// </summary>
/// <param name="Start">First sample meeting the criteria.</param>
/// <param name="Best">When Mercury is highest. In practice, as the Sun reaches the limit: the start at dusk, the end at dawn.</param>
/// <param name="End">Last sample meeting the criteria.</param>
/// <param name="Magnitude">How bright Mercury is at the best moment.</param>
public sealed record MercuryWindow(TwilightPeriod Period, MercuryPoint Start, MercuryPoint Best, MercuryPoint End, double Magnitude)
{
    public TimeSpan Duration => End.Instant - Start.Instant;
}

/// <summary>
/// A season of Mercury: the days in a row when it can be seen, at dusk or at dawn, around one of its greatest elongations.
/// </summary>
/// <param name="Best">The day it is highest, the one to recommend.</param>
/// <param name="First">The first day of the season.</param>
/// <param name="Last">The last day of the season.</param>
/// <param name="Days">How many days it can be seen.</param>
public sealed record MercuryApparition(TwilightPeriod Period, MercuryWindow Best, MercuryWindow First, MercuryWindow Last, int Days);
