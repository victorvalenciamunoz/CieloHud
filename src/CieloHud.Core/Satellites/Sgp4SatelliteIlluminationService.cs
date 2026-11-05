using SGPdotNET.Propagation.Bodies;
using SGPdotNET.Util;

namespace CieloHud.Core.Satellites;

/// <summary>
/// <see cref="ISatelliteIlluminationService"/> using SGP.NET for both the satellite and the Sun in the same ECI frame,
/// and <see cref="EarthShadow"/> for the umbra test.
/// </summary>
public sealed class Sgp4SatelliteIlluminationService : ISatelliteIlluminationService
{
    public bool IsSunlit(Tle tle, DateTimeOffset instant)
    {
        var utc = instant.UtcDateTime;
        var satellite = ToEci(SgpConversions.ToSatellite(tle).Predict(utc).Position);
        var sun = ToEci(Sun.Predict(utc).Position);
        return !EarthShadow.IsInUmbra(satellite, sun);
    }

    private static EciPosition ToEci(Vector3 v) => new(v.X, v.Y, v.Z);
}
