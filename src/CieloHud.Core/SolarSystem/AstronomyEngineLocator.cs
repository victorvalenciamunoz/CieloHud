using CieloHud.Core.Sky;
using CosineKitty;
using AeObserver = CosineKitty.Observer;
using Observer = CieloHud.Core.Sky.Observer;

namespace CieloHud.Core.SolarSystem;

/// <summary>
/// Shared Astronomy Engine call: topocentric, aberration-corrected, equator of date.
/// </summary>
internal static class AstronomyEngineLocator
{
    private const double KmPerAstronomicalUnit = 149_597_870.7;

    public static HorizontalPosition Locate(Body body, Observer observer, DateTimeOffset instant, Refraction refraction)
    {
        var time = new AstroTime(instant.UtcDateTime);
        var aeObserver = new AeObserver(observer.LatitudeDegrees, observer.LongitudeDegrees, observer.AltitudeMeters);

        var equatorial = Astronomy.Equator(body, time, aeObserver, EquatorEpoch.OfDate, Aberration.Corrected);
        var topocentric = Astronomy.Horizon(time, aeObserver, equatorial.ra, equatorial.dec, refraction);

        return new HorizontalPosition(
            azimuthDegrees: topocentric.azimuth,
            altitudeDegrees: topocentric.altitude,
            distanceKm: equatorial.dist * KmPerAstronomicalUnit);
    }

    public static Body ToBody(CelestialBody body) => body switch
    {
        CelestialBody.Moon => Body.Moon,
        CelestialBody.Mercury => Body.Mercury,
        CelestialBody.Venus => Body.Venus,
        CelestialBody.Mars => Body.Mars,
        CelestialBody.Jupiter => Body.Jupiter,
        CelestialBody.Saturn => Body.Saturn,
        _ => throw new ArgumentOutOfRangeException(nameof(body), body, "Unsupported celestial body."),
    };
}
