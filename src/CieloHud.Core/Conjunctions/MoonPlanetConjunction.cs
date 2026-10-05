using CieloHud.Core.Sky;
using CieloHud.Core.SolarSystem;

namespace CieloHud.Core.Conjunctions;

/// <summary>The Moon and a planet at one instant, as seen from the observer.</summary>
/// <param name="SeparationDegrees">Angle between them on the sky, from their apparent (refracted) positions: what the eye sees.</param>
public readonly record struct ConjunctionPoint(
    DateTimeOffset Instant,
    HorizontalPosition Moon,
    HorizontalPosition Planet,
    double SeparationDegrees)
{
    /// <summary>The altitude of the lower of the two.</summary>
    public double LowerAltitudeDegrees => Math.Min(Moon.AltitudeDegrees, Planet.AltitudeDegrees);
}

/// <summary>
/// One night when the Moon passes close to a planet and both can be seen: the window when the criteria hold,
/// the best moment to look, and the closest approach within the window.
/// </summary>
/// <param name="Start">First sample meeting the criteria (or the start of the search, if it was already met).</param>
/// <param name="Best">When to look: see <see cref="ConjunctionFinder"/>.</param>
/// <param name="Closest">Smallest separation within the window; may be low over the horizon.</param>
/// <param name="End">Last sample meeting the criteria.</param>
public sealed record MoonPlanetConjunction(
    CelestialBody Planet,
    ConjunctionPoint Start,
    ConjunctionPoint Best,
    ConjunctionPoint Closest,
    ConjunctionPoint End)
{
    public TimeSpan Duration => End.Instant - Start.Instant;
}
