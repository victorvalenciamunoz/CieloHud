namespace CieloHud.Core.Tests.Satellites;

/// <summary>
/// Reference values from JPL Horizons (COMMAND='-125544', observer at Madrid, APPARENT='AIRLESS', QUANTITIES='4,20'),
/// queried on 2026-10-02. docs/PLAN.md allows 1° for the ISS with a recent TLE; measured differences with this TLE
/// (16-22 h old at the test instants) were below 0.01°, so the tolerance is ten times tighter than the target.
/// </summary>
public class Sgp4SatelliteServiceTests
{
    private const double KmPerAstronomicalUnit = 149_597_870.7;
    private const double AngleToleranceDegrees = 0.1;
    private const double DistanceToleranceRatio = 0.005;

    private static readonly Observer Madrid = new(latitudeDegrees: 40.4168, longitudeDegrees: -3.7038, altitudeMeters: 650);

    private readonly Sgp4SatelliteService _service = new();

    [Theory]
    [InlineData("2026-10-02T11:26:00Z", 334.626668, 47.346300, 0.00000379254785)]
    [InlineData("2026-10-02T17:56:00Z", 214.449465, 31.632295, 0.00000499613885)]
    public void Locate_Iss_MatchesJplHorizons(string instant, double expectedAzimuth, double expectedAltitude, double expectedDistanceAu)
    {
        var position = _service.Locate(TleTests.Iss, Madrid, DateTimeOffset.Parse(instant));

        Assert.InRange(position.AzimuthDegrees, expectedAzimuth - AngleToleranceDegrees, expectedAzimuth + AngleToleranceDegrees);
        Assert.InRange(position.AltitudeDegrees, expectedAltitude - AngleToleranceDegrees, expectedAltitude + AngleToleranceDegrees);

        var expectedDistanceKm = expectedDistanceAu * KmPerAstronomicalUnit;
        Assert.NotNull(position.DistanceKm);
        Assert.InRange(position.DistanceKm.Value, expectedDistanceKm * (1 - DistanceToleranceRatio), expectedDistanceKm * (1 + DistanceToleranceRatio));
    }

    [Fact]
    public void Locate_IgnoresLocalOffset_UsesInstant()
    {
        var utc = _service.Locate(TleTests.Iss, Madrid, DateTimeOffset.Parse("2026-10-02T11:26:00Z"));
        var madridLocal = _service.Locate(TleTests.Iss, Madrid, DateTimeOffset.Parse("2026-10-02T13:26:00+02:00"));

        Assert.Equal(utc, madridLocal);
    }

    [Fact]
    public void Locate_BelowHorizon_ReportsNegativeAltitude()
    {
        // Horizons: 2026-10-02 21:00 UTC, az 289.48°, el -16.91° (airless).
        var position = _service.Locate(TleTests.Iss, Madrid, DateTimeOffset.Parse("2026-10-02T21:00:00Z"));

        Assert.False(position.IsAboveHorizon);
        Assert.InRange(position.AltitudeDegrees, -16.91 - AngleToleranceDegrees, -16.91 + AngleToleranceDegrees);
    }
}
