using CieloHud.Core.Satellites;
using CieloHud.Core.Sky;
using SGPdotNET.Observation;
using SGPdotNET.Util;

namespace CieloHud.Core.Passes;

/// <summary>
/// <see cref="ISatellitePassPredictor"/> backed by SGP.NET's range observation, which steps through time
/// and refines each horizon crossing to the second.
/// </summary>
public sealed class Sgp4SatellitePassPredictor : ISatellitePassPredictor
{
    // ISS passes last several minutes above 0°, so 10 s cannot miss one. 24 h = 8640 propagations, well under a second.
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(10);

    public IReadOnlyList<SatellitePass> Predict(Tle tle, Observer observer, DateTimeOffset from, DateTimeOffset to)
    {
        if (to <= from)
            throw new ArgumentException("'to' must be later than 'from'.", nameof(to));

        var satellite = SgpConversions.ToSatellite(tle);
        var groundStation = SgpConversions.ToGroundStation(observer);

        var periods = groundStation.Observe(
            satellite, from.UtcDateTime, to.UtcDateTime, Step, Angle.Zero,
            clipToStartTime: true, clipToEndTime: false, resolution: 0);

        return periods
            .Select(p => new SatellitePass(
                PointAt(groundStation, satellite, p.Start),
                PointAt(groundStation, satellite, p.MaxElevationTime),
                PointAt(groundStation, satellite, p.End)))
            .ToList();
    }

    private static PassPoint PointAt(GroundStation groundStation, Satellite satellite, DateTime utc) =>
        new(SgpConversions.ToInstant(utc), SgpConversions.ToHorizontalPosition(groundStation.Observe(satellite, utc)));
}
