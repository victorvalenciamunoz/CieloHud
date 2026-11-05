using CieloHud.Core.Tests.Satellites;

namespace CieloHud.Core.Tests.Cards;

/// <summary>
/// Reference values from JPL Horizons (https://ssd.jpl.nasa.gov/api/horizons.api), queried on 2026-10-07 for the ISS
/// (COMMAND='-125544') from the center of the Earth (CENTER='500@399'), EPHEM_TYPE='VECTORS', TIME_TYPE='UT', OUT_UNITS='KM-S':
/// <list type="bullet">
/// <item>Height: the position in the Earth-fixed frame (REF_PLANE='BODY EQUATOR', ITRF93) turned into height over the WGS-84
/// ellipsoid (a = 6378.137 km, f = 1/298.257223563, iterating on the latitude).</item>
/// <item>Speed: the length of the velocity in the inertial frame (REF_PLANE='FRAME', ICRF).</item>
/// </list>
/// The TLE is the fixed one of the tests (epoch 2026-10-01 19:41 UTC), 16 h to 31 h before the instants.
/// </summary>
public class SatelliteFactsIntegrationTests
{
    // Measured: height within 0.06 km, speed within 0.1 m/s.
    private const double HeightToleranceKm = 0.5;
    private const double SpeedToleranceKmPerSecond = 0.005;

    private static readonly Observer Madrid = new(latitudeDegrees: 40.4168, longitudeDegrees: -3.7038, altitudeMeters: 650);

    private readonly Sgp4SatelliteFactsService _service =
        new(new Sgp4SatelliteService(), new Sgp4SatelliteIlluminationService(), new AstronomyEngineSunService());

    [Theory]
    [InlineData("2026-10-02T11:26:00Z", 428.83, 7.6574)]
    [InlineData("2026-10-02T17:56:00Z", 422.55, 7.6629)]
    [InlineData("2026-10-02T21:00:00Z", 424.07, 7.6621)]
    [InlineData("2026-10-03T03:00:00Z", 429.68, 7.6581)]
    public void HeightAndSpeed_MatchHorizons(string instant, double heightKm, double speedKmPerSecond)
    {
        var facts = _service.Satellite(TleTests.Iss, Madrid, DateTimeOffset.Parse(instant));

        Assert.InRange(facts.AltitudeKm, heightKm - HeightToleranceKm, heightKm + HeightToleranceKm);
        Assert.InRange(facts.SpeedKmPerSecond, speedKmPerSecond - SpeedToleranceKmPerSecond, speedKmPerSecond + SpeedToleranceKmPerSecond);
    }

    [Fact]
    public void Distance_TheSameAsTheHud()
    {
        // Horizons, observer table from Madrid at 11:26 (Sgp4SatelliteServiceTests): 0.00000379254785 au = 567.36 km.
        var facts = _service.Satellite(TleTests.Iss, Madrid, DateTimeOffset.Parse("2026-10-02T11:26:00Z"));

        Assert.InRange(facts.DistanceKm, 567.36 * 0.995, 567.36 * 1.005);
    }

    [Fact]
    public void Period_FromTheElements()
    {
        // Line 2 gives 15.48706258 turns a day: 92.98 minutes. SGP4 recovers the mean motion from it.
        var facts = _service.Satellite(TleTests.Iss, Madrid, DateTimeOffset.Parse("2026-10-02T11:26:00Z"));

        Assert.InRange(facts.Period.TotalMinutes, 92.9, 93.1);
    }

    /// <summary>
    /// Oct 2 at 19:33 the grazing dusk pass, just risen (lit, Sun at −19°, see VisiblePassFinderIntegrationTests); Oct 15 at dawn the ISS
    /// rises in the Earth's shadow and comes out of it at 4:25.
    /// </summary>
    [Theory]
    [InlineData("2026-10-02T11:26:00Z", SatelliteSight.SkyTooBright)]
    [InlineData("2026-10-02T19:33:00Z", SatelliteSight.Visible)]
    [InlineData("2026-10-02T21:00:00Z", SatelliteSight.BelowHorizon)]
    [InlineData("2026-10-15T04:23:00Z", SatelliteSight.InEarthShadow)]
    [InlineData("2026-10-15T04:28:00Z", SatelliteSight.Visible)]
    public void Sight_ByTheRulesOfAVisiblePass(string instant, SatelliteSight sight)
    {
        Assert.Equal(sight, _service.Satellite(TleTests.Iss, Madrid, DateTimeOffset.Parse(instant)).Sight);
    }
}
