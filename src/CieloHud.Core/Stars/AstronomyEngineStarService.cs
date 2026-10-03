using CieloHud.Core.Sky;
using CosineKitty;
using AeObserver = CosineKitty.Observer;
using Observer = CieloHud.Core.Sky.Observer;

namespace CieloHud.Core.Stars;

/// <summary>
/// <see cref="IStarService"/> backed by Astronomy Engine: J2000 catalog direction rotated to the true equator of date
/// (precession + nutation), then to the observer's horizon with normal refraction, like the planets.
/// Annual aberration (up to 20") and proper motion are ignored; both are far below the sensor accuracy.
/// </summary>
public sealed class AstronomyEngineStarService : IStarService
{
    public HorizontalPosition Locate(Star star, Observer observer, DateTimeOffset instant)
    {
        var time = new AstroTime(instant.UtcDateTime);
        var j2000 = Astronomy.VectorFromSphere(new Spherical(star.DeclinationDegrees, star.RightAscensionDegrees, 1), time);
        var ofDate = Astronomy.RotateVector(Astronomy.Rotation_EQJ_EQD(time), j2000);
        var equatorial = Astronomy.EquatorFromVector(ofDate);

        var aeObserver = new AeObserver(observer.LatitudeDegrees, observer.LongitudeDegrees, observer.AltitudeMeters);
        var topocentric = Astronomy.Horizon(time, aeObserver, equatorial.ra, equatorial.dec, Refraction.Normal);

        return new HorizontalPosition(topocentric.azimuth, topocentric.altitude);
    }
}
