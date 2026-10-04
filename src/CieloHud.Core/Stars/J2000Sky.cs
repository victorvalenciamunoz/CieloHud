using CieloHud.Core.Sky;
using CosineKitty;
using AeObserver = CosineKitty.Observer;
using Observer = CieloHud.Core.Sky.Observer;

namespace CieloHud.Core.Stars;

/// <summary>
/// Fixed J2000 directions (stars, constellation lines) to the observer's horizon: rotated to the true equator of date
/// (precession + nutation), then to azimuth/altitude with normal refraction, like the planets.
/// Annual aberration (up to 20") and proper motion are ignored; both are far below the sensor accuracy.
/// </summary>
internal static class J2000Sky
{
    public static HorizontalPosition Locate(double rightAscensionDegrees, double declinationDegrees, Observer observer, DateTimeOffset instant)
    {
        var time = new AstroTime(instant.UtcDateTime);
        var aeObserver = new AeObserver(observer.LatitudeDegrees, observer.LongitudeDegrees, observer.AltitudeMeters);
        var toDate = Astronomy.Rotation_EQJ_EQD(time);
        return Locate(rightAscensionDegrees, declinationDegrees, time, aeObserver, toDate);
    }

    /// <summary>Many directions at the same instant share the observer and the precession matrix.</summary>
    public static IReadOnlyList<HorizontalPosition> LocateAll(IEnumerable<(double Ra, double Dec)> directions, Observer observer, DateTimeOffset instant)
    {
        var time = new AstroTime(instant.UtcDateTime);
        var aeObserver = new AeObserver(observer.LatitudeDegrees, observer.LongitudeDegrees, observer.AltitudeMeters);
        var toDate = Astronomy.Rotation_EQJ_EQD(time);
        return directions.Select(d => Locate(d.Ra, d.Dec, time, aeObserver, toDate)).ToList();
    }

    private static HorizontalPosition Locate(double ra, double dec, AstroTime time, AeObserver observer, RotationMatrix toDate)
    {
        var j2000 = Astronomy.VectorFromSphere(new Spherical(dec, ra, 1), time);
        var equatorial = Astronomy.EquatorFromVector(Astronomy.RotateVector(toDate, j2000));
        var topocentric = Astronomy.Horizon(time, observer, equatorial.ra, equatorial.dec, Refraction.Normal);
        return new HorizontalPosition(topocentric.azimuth, topocentric.altitude);
    }
}
