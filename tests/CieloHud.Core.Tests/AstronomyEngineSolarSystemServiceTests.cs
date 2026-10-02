namespace CieloHud.Core.Tests;

/// <summary>
/// Reference values come from JPL Horizons (https://ssd.jpl.nasa.gov/api/horizons.api), observer table,
/// CENTER='coord@399', SITE_COORD='-3.7038,40.4168,0.650', QUANTITIES='4,20', APPARENT='REFRACTED'.
/// Queried on 2026-10-02. Horizons gives refracted apparent azimuth/elevation and the AU distance (delta).
/// </summary>
public class AstronomyEngineSolarSystemServiceTests
{
    private const double KmPerAstronomicalUnit = 149_597_870.7;

    // Ten times tighter than the 0.5° target in docs/PLAN.md; measured differences are below 0.005°.
    private const double AngleToleranceDegrees = 0.05;
    private const double DistanceToleranceRatio = 0.001;

    private static readonly Observer Madrid = new(latitudeDegrees: 40.4168, longitudeDegrees: -3.7038, altitudeMeters: 650);

    private readonly AstronomyEngineSolarSystemService _service = new();

    [Theory]
    [InlineData(CelestialBody.Moon, "2026-10-03T04:00:00Z", 111.410231, 63.749880, 0.00243358446205)]
    [InlineData(CelestialBody.Jupiter, "2026-10-03T04:00:00Z", 87.899896, 21.721691, 5.89525566054929)]
    [InlineData(CelestialBody.Saturn, "2026-10-02T21:00:00Z", 118.107227, 31.712032, 8.43468370191029)]
    public void Locate_MatchesJplHorizons(CelestialBody body, string instant, double expectedAzimuth, double expectedAltitude, double expectedDistanceAu)
    {
        var position = _service.Locate(body, Madrid, DateTimeOffset.Parse(instant));

        Assert.InRange(position.AzimuthDegrees, expectedAzimuth - AngleToleranceDegrees, expectedAzimuth + AngleToleranceDegrees);
        Assert.InRange(position.AltitudeDegrees, expectedAltitude - AngleToleranceDegrees, expectedAltitude + AngleToleranceDegrees);

        var expectedDistanceKm = expectedDistanceAu * KmPerAstronomicalUnit;
        Assert.NotNull(position.DistanceKm);
        Assert.InRange(position.DistanceKm.Value, expectedDistanceKm * (1 - DistanceToleranceRatio), expectedDistanceKm * (1 + DistanceToleranceRatio));
    }

    [Fact]
    public void Locate_IgnoresLocalOffset_UsesInstant()
    {
        var utc = _service.Locate(CelestialBody.Saturn, Madrid, DateTimeOffset.Parse("2026-10-02T21:00:00Z"));
        var madridLocal = _service.Locate(CelestialBody.Saturn, Madrid, DateTimeOffset.Parse("2026-10-02T23:00:00+02:00"));

        Assert.Equal(utc, madridLocal);
    }

    [Fact]
    public void Locate_SupportsEveryCelestialBody()
    {
        var instant = DateTimeOffset.Parse("2026-10-02T21:00:00Z");

        foreach (var body in Enum.GetValues<CelestialBody>())
        {
            var position = _service.Locate(body, Madrid, instant);

            Assert.InRange(position.AzimuthDegrees, 0, 360);
            Assert.InRange(position.AltitudeDegrees, -90, 90);
            Assert.True(position.DistanceKm > 0, $"{body} should have a positive distance.");
        }
    }
}
