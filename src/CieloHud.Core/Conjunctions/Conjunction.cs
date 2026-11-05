using CieloHud.Core.Sky;
using CieloHud.Core.SolarSystem;

namespace CieloHud.Core.Conjunctions;

/// <summary>The two bodies of a conjunction at one instant, as seen from the observer.</summary>
/// <param name="Guide">Where the guide body is: the Moon, or the brighter of two planets.</param>
/// <param name="Companion">Where the other body is.</param>
/// <param name="SeparationDegrees">Angle between them on the sky, from their apparent (refracted) positions: what the eye sees.</param>
public readonly record struct ConjunctionPoint(
    DateTimeOffset Instant,
    HorizontalPosition Guide,
    HorizontalPosition Companion,
    double SeparationDegrees)
{
    /// <summary>The altitude of the lower of the two.</summary>
    public double LowerAltitudeDegrees => Math.Min(Guide.AltitudeDegrees, Companion.AltitudeDegrees);
}

/// <summary>The first and last nights, in a row, that two planets are close enough (their windows' starts).</summary>
public readonly record struct ConjunctionNights(DateTimeOffset First, DateTimeOffset Last);

/// <summary>
/// One night when two bodies are close together and both can be seen: the window when the criteria hold, the best moment
/// to look, and the closest approach within the window. The guide is what the HUD leads to: the Moon, or the brighter planet.
/// </summary>
/// <param name="Start">First sample meeting the criteria (or the first sample of the search, if it was already met).</param>
/// <param name="Best">When to look: see <see cref="ConjunctionFinder"/>.</param>
/// <param name="Closest">Smallest separation within the window; may be low over the horizon.</param>
/// <param name="End">Last sample meeting the criteria.</param>
/// <param name="Nights">For two planets, which close for many nights in a row: all those nights. This one is the closest. Null for the Moon.</param>
public sealed record Conjunction(
    CelestialBody Guide,
    CelestialBody Companion,
    ConjunctionPoint Start,
    ConjunctionPoint Best,
    ConjunctionPoint Closest,
    ConjunctionPoint End,
    ConjunctionNights? Nights = null)
{
    public bool IsWithMoon => Guide == CelestialBody.Moon;

    public TimeSpan Duration => End.Instant - Start.Instant;
}
