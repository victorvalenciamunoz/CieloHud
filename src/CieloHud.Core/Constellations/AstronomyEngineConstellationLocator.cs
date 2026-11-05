using CosineKitty;
using AeObserver = CosineKitty.Observer;
using Observer = CieloHud.Core.Sky.Observer;

namespace CieloHud.Core.Constellations;

/// <summary>
/// <see cref="IConstellationLocator"/> backed by Astronomy Engine: the horizontal direction is turned back into J2000
/// equatorial coordinates (removing refraction), then looked up in the official IAU boundaries (Roman 1987, as
/// implemented by <c>Astronomy.Constellation</c>). Works for any direction, including empty sky and below the horizon.
/// </summary>
public sealed class AstronomyEngineConstellationLocator : IConstellationLocator
{
    public Constellation Locate(double azimuthDegrees, double altitudeDegrees, Observer observer, DateTimeOffset instant)
    {
        var time = new AstroTime(instant.UtcDateTime);
        var aeObserver = new AeObserver(observer.LatitudeDegrees, observer.LongitudeDegrees, observer.AltitudeMeters);

        // Remove refraction with the closed-form lift evaluated at the apparent altitude (error ~0.01° near the horizon,
        // far below the compass). Astronomy Engine 2.1.19's iterative inverse never returns for some inputs
        // (altitude exactly 90°, or well below the horizon such as -71°), so VectorFromHorizon is never asked to invert it.
        var geometricAltitude = altitudeDegrees - Astronomy.RefractionAngle(Refraction.Normal, altitudeDegrees);
        var horizontal = Astronomy.VectorFromHorizon(new Spherical(geometricAltitude, azimuthDegrees, 1), time, Refraction.None);
        var j2000 = Astronomy.RotateVector(Astronomy.Rotation_HOR_EQJ(time, aeObserver), horizontal);
        var equatorial = Astronomy.EquatorFromVector(j2000);

        var info = Astronomy.Constellation(equatorial.ra, equatorial.dec);
        return new Constellation(info.Symbol, info.Name);
    }
}
