using CieloHud.Core.SolarSystem;

namespace CieloHud.Core.Cards;

/// <summary>A planet right now, for its card.</summary>
/// <param name="DistanceKm">From the observer to the planet.</param>
/// <param name="LightTime">How long ago the light now reaching the observer left the planet.</param>
public sealed record PlanetFacts(CelestialBody Body, double DistanceKm, TimeSpan LightTime);

/// <summary>Light travel time over a distance.</summary>
public static class LightTravel
{
    /// <summary>Speed of light in vacuum, exact by definition of the meter.</summary>
    public const double SpeedOfLightKmPerSecond = 299_792.458;

    public static TimeSpan Time(double distanceKm) => TimeSpan.FromSeconds(distanceKm / SpeedOfLightKmPerSecond);
}
