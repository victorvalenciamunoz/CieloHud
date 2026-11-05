using CieloHud.Core.Sky;

namespace CieloHud.Core.Satellites;

/// <summary>
/// <see cref="ISatelliteService"/> backed by SGP.NET's SGP4/SDP4 propagator.
/// </summary>
public sealed class Sgp4SatelliteService : ISatelliteService
{
    public HorizontalPosition Locate(Tle tle, Observer observer, DateTimeOffset instant)
    {
        var satellite = SgpConversions.ToSatellite(tle);
        var groundStation = SgpConversions.ToGroundStation(observer);

        // SGP.NET expects a UTC DateTime; DateTimeOffset.UtcDateTime has Kind == Utc.
        var observation = groundStation.Observe(satellite, instant.UtcDateTime);

        return SgpConversions.ToHorizontalPosition(observation);
    }
}
