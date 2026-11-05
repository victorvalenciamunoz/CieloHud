using CieloHud.Core.SolarSystem;

namespace CieloHud.Core.Conjunctions;

/// <summary>
/// What counts as a conjunction worth going out for: the Moon next to a planet (decision 025) or two planets together
/// (decision 028). Close together, both clear of buildings and haze, and a dark enough sky. Fixed by design, like the pass criteria.
/// </summary>
public sealed record ConjunctionCriteria
{
    /// <summary>Largest Moon-planet separation, as seen from the observer: about three fingers at arm's length.</summary>
    public double MaxMoonSeparationDegrees { get; init; } = 5;

    /// <summary>
    /// Largest separation between two planets: two points of light about two fingers apart. Stricter than for the Moon, which is
    /// big and bright enough to look "next to" a planet five degrees away.
    /// </summary>
    public double MaxPlanetSeparationDegrees { get; init; } = 3;

    /// <summary>
    /// Two planets stay close for many nights (Mars and Jupiter in Nov 2026: 15 nights within 3°). To tell which night is the
    /// closest, the search looks this far before and after the requested range. Longer approaches (Jupiter and Saturn, every 20 years)
    /// would need more.
    /// </summary>
    public TimeSpan PlanetApproachSearch { get; init; } = TimeSpan.FromDays(60);

    /// <summary>Both bodies must be at least this high.</summary>
    public double MinAltitudeDegrees { get; init; } = 10;

    /// <summary>
    /// When choosing the best moment, prefer instants with both at least this high, if there are any:
    /// the closest approach often falls at the edge of the window, low over the horizon.
    /// </summary>
    public double ComfortableAltitudeDegrees { get; init; } = 20;

    /// <summary>The Sun must be below this geometric altitude (civil twilight), as for ISS passes.</summary>
    public double MaxSunAltitudeDegrees { get; init; } = -6;

    /// <summary>Sampling step. The Moon moves about 0.5° an hour against the planets: 5 minutes is 0.04°.</summary>
    public TimeSpan Step { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Two windows of the Moon with the same planet closer than this are the same approach (the Moon moves 13° a day, so with a 5°
    /// limit it can only be the dawn and the dusk of one day); only the closer one is kept. Two planets are grouped night by night instead.
    /// </summary>
    public TimeSpan SameApproachWithin { get; init; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Planets looked at, with the Moon and among themselves. Mercury is left out: from a city it never meets these criteria
    /// (low, in twilight, faint).
    /// </summary>
    public IReadOnlyList<CelestialBody> Planets { get; init; } =
        [CelestialBody.Venus, CelestialBody.Mars, CelestialBody.Jupiter, CelestialBody.Saturn];

    public static ConjunctionCriteria Default { get; } = new();

    internal void Validate()
    {
        if (MaxMoonSeparationDegrees <= 0 || MaxPlanetSeparationDegrees <= 0)
            throw new ArgumentException("Separations must be positive.");
        if (PlanetApproachSearch < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(PlanetApproachSearch), PlanetApproachSearch, "Cannot be negative.");
        if (ComfortableAltitudeDegrees < MinAltitudeDegrees)
            throw new ArgumentException("Expected ComfortableAltitudeDegrees >= MinAltitudeDegrees.");
        if (Step <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(Step), Step, "Step must be positive.");
        if (Planets.Contains(CelestialBody.Moon))
            throw new ArgumentException("The Moon cannot be one of the planets.", nameof(Planets));
    }
}
