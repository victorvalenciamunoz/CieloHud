using CieloHud.Core.Sky;
using SGPdotNET.CoordinateSystem;
using SGPdotNET.Observation;
using SGPdotNET.Util;
using SgpTle = SGPdotNET.TLE.Tle;

namespace CieloHud.Core.Satellites;

/// <summary>Conversions between CieloHud types and SGP.NET types.</summary>
internal static class SgpConversions
{
    private const double MetersPerKilometer = 1000;

    public static Satellite ToSatellite(Tle tle) => new(new SgpTle(tle.Name, tle.Line1, tle.Line2));

    public static GroundStation ToGroundStation(Observer observer) => new(new GeodeticCoordinate(
        Angle.FromDegrees(observer.LatitudeDegrees),
        Angle.FromDegrees(observer.LongitudeDegrees),
        observer.AltitudeMeters / MetersPerKilometer));

    public static HorizontalPosition ToHorizontalPosition(TopocentricObservation observation) => new(
        azimuthDegrees: observation.Azimuth.Degrees,
        altitudeDegrees: observation.Elevation.Degrees,
        distanceKm: observation.Range);

    /// <summary>SGP.NET works in UTC <see cref="DateTime"/>; this restores the offset-aware instant.</summary>
    public static DateTimeOffset ToInstant(DateTime utc) => new(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
}
