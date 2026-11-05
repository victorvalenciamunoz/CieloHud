namespace CieloHud.Core.Tests.SolarSystem;

/// <summary>
/// Reference values from JPL Horizons (COMMAND='10', observer at Madrid, APPARENT='AIRLESS', QUANTITIES='4,20'), queried on 2026-10-02.
/// Airless because the Sun service returns geometric altitude.
/// </summary>
public class AstronomyEngineSunServiceTests
{
    private const double AngleToleranceDegrees = 0.05;

    private static readonly Observer Madrid = new(latitudeDegrees: 40.4168, longitudeDegrees: -3.7038, altitudeMeters: 650);

    private readonly AstronomyEngineSunService _service = new();

    [Theory]
    [InlineData("2026-10-02T12:00:00Z", 178.512369, 45.880644)]  // day
    [InlineData("2026-10-02T18:30:00Z", 271.309116, -7.396977)]  // nautical twilight, just past civil
    [InlineData("2026-10-02T21:00:00Z", 299.029172, -34.821148)] // night
    public void Locate_MatchesJplHorizons_Geometric(string instant, double expectedAzimuth, double expectedAltitude)
    {
        var position = _service.Locate(Madrid, DateTimeOffset.Parse(instant));

        Assert.InRange(position.AzimuthDegrees, expectedAzimuth - AngleToleranceDegrees, expectedAzimuth + AngleToleranceDegrees);
        Assert.InRange(position.AltitudeDegrees, expectedAltitude - AngleToleranceDegrees, expectedAltitude + AngleToleranceDegrees);
    }

    [Fact]
    public void Locate_ReportsDistanceAboutOneAstronomicalUnit()
    {
        var position = _service.Locate(Madrid, DateTimeOffset.Parse("2026-10-02T12:00:00Z"));

        // Horizons: 1.00087650 AU.
        Assert.NotNull(position.DistanceKm);
        Assert.InRange(position.DistanceKm.Value / 149_597_870.7, 1.0007, 1.0010);
    }
}
