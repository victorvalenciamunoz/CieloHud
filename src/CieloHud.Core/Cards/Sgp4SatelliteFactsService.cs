using CieloHud.Core.Passes;
using CieloHud.Core.Satellites;
using CieloHud.Core.Sky;
using CieloHud.Core.SolarSystem;

namespace CieloHud.Core.Cards;

/// <summary>
/// <see cref="ISatelliteFactsService"/> with SGP.NET's SGP4: height from its geodetic conversion, speed from its inertial
/// velocity, period from the elements. Whether it shows comes from the same services and limits as the visible passes.
/// </summary>
public sealed class Sgp4SatelliteFactsService(
    ISatelliteService satellites, ISatelliteIlluminationService illumination, ISunService sun, VisibilityCriteria? criteria = null)
    : ISatelliteFactsService
{
    private readonly VisibilityCriteria _criteria = criteria ?? VisibilityCriteria.Default;

    public SatelliteFacts Satellite(Tle tle, Observer observer, DateTimeOffset instant)
    {
        var satellite = SgpConversions.ToSatellite(tle);
        var state = satellite.Predict(instant.UtcDateTime);
        var v = state.Velocity;
        var position = satellites.Locate(tle, observer, instant);

        return new SatelliteFacts(
            AltitudeKm: state.ToGeodetic().Altitude,
            DistanceKm: position.DistanceKm ?? throw new InvalidOperationException("A satellite's position always has a distance."),
            SpeedKmPerSecond: Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z),
            Period: TimeSpan.FromMinutes(satellite.Orbit.Period),
            Sight: SatelliteSights.Of(
                position.AltitudeDegrees, illumination.IsSunlit(tle, instant), sun.Locate(observer, instant).AltitudeDegrees, _criteria));
    }
}
