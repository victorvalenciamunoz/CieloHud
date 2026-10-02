using CieloHud.Core.Sky;
using SGPdotNET.CoordinateSystem;
using SGPdotNET.Observation;
using SGPdotNET.Util;
using SgpTle = SGPdotNET.TLE.Tle;

namespace CieloHud.Core.Satellites;

/// <summary>
/// <see cref="ISatelliteService"/> backed by SGP.NET's SGP4/SDP4 propagator.
/// </summary>
public sealed class Sgp4SatelliteService : ISatelliteService
{
    private const double MetersPerKilometer = 1000;

    public HorizontalPosition Locate(Tle tle, Observer observer, DateTimeOffset instant)
    {
        var satellite = new Satellite(new SgpTle(tle.Name, tle.Line1, tle.Line2));
        var groundStation = new GroundStation(new GeodeticCoordinate(
            Angle.FromDegrees(observer.LatitudeDegrees),
            Angle.FromDegrees(observer.LongitudeDegrees),
            observer.AltitudeMeters / MetersPerKilometer));

        // SGP.NET expects a UTC DateTime; DateTimeOffset.UtcDateTime has Kind == Utc.
        var observation = groundStation.Observe(satellite, instant.UtcDateTime);

        return new HorizontalPosition(
            azimuthDegrees: observation.Azimuth.Degrees,
            altitudeDegrees: observation.Elevation.Degrees,
            distanceKm: observation.Range);
    }
}
