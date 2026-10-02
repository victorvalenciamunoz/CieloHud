using CosineKitty;
using AeObserver = CosineKitty.Observer;

namespace CieloHud.Core;

/// <summary>
/// <see cref="ISolarSystemService"/> backed by Astronomy Engine (CosineKitty.AstronomyEngine).
/// Returns topocentric, aberration-corrected positions with normal atmospheric refraction,
/// so altitudes match what the eye sees and what Stellarium shows with its atmosphere on.
/// </summary>
public sealed class AstronomyEngineSolarSystemService : ISolarSystemService
{
    private const double KmPerAstronomicalUnit = 149_597_870.7;

    public HorizontalPosition Locate(CelestialBody body, Observer observer, DateTimeOffset instant)
    {
        var time = new AstroTime(instant.UtcDateTime);
        var aeObserver = new AeObserver(observer.LatitudeDegrees, observer.LongitudeDegrees, observer.AltitudeMeters);

        var equatorial = Astronomy.Equator(ToAeBody(body), time, aeObserver, EquatorEpoch.OfDate, Aberration.Corrected);
        var topocentric = Astronomy.Horizon(time, aeObserver, equatorial.ra, equatorial.dec, Refraction.Normal);

        return new HorizontalPosition(
            azimuthDegrees: topocentric.azimuth,
            altitudeDegrees: topocentric.altitude,
            distanceKm: equatorial.dist * KmPerAstronomicalUnit);
    }

    private static Body ToAeBody(CelestialBody body) => body switch
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
