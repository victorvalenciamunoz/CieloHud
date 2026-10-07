namespace CieloHud.Core.Tests.Cards;

/// <summary>
/// Reference values from JPL Horizons (https://ssd.jpl.nasa.gov/api/horizons.api), queried on 2026-10-07, for Saturn (699) from
/// the center of the Earth (CENTER='500@399'). Horizons has no ring tilt, so it is derived two independent ways that agree:
/// <list type="bullet">
/// <item>From the Earth: the observer's sub-latitude (QUANTITIES='14'), which is planetodetic, made planetocentric with Saturn's
/// radii (tan φc = (54364 / 60268)² tan φd); and the same from the astrometric direction (QUANTITIES='1') against the IAU pole
/// (α0 = 40.589 − 0.036 T, δ0 = 83.537 − 0.004 T). Both to 0.001°.</item>
/// <item>From the Sun: the Sun's sub-latitude (QUANTITIES='15'), made planetocentric, sampled monthly around the next widest rings
/// (2031-2033) and the next edge-on (2038-2040), and around the last one (2025).</item>
/// </list>
/// </summary>
public class SaturnRingsIntegrationTests
{
    // Measured: from the Earth within 0.02°, from the Sun within 0.006°.
    private const double TiltTolerance = 0.05;

    private readonly AstronomyEngineSolarSystemFactsService _service = new();

    [Theory]
    [InlineData("2025-03-23T00:00:00Z", 0.042)]
    [InlineData("2025-11-24T00:00:00Z", -0.368)]
    [InlineData("2025-12-24T00:00:00Z", -0.734)]
    [InlineData("2026-10-07T00:00:00Z", -7.358)]
    [InlineData("2026-11-06T00:00:00Z", -6.454)]
    [InlineData("2027-06-01T00:00:00Z", -13.183)]
    [InlineData("2027-07-01T00:00:00Z", -14.075)]
    [InlineData("2028-12-01T00:00:00Z", -17.152)]
    [InlineData("2028-12-31T00:00:00Z", -16.895)]
    [InlineData("2032-05-01T00:00:00Z", -26.927)]
    [InlineData("2032-05-31T00:00:00Z", -26.906)]
    public void Tilt_FromTheEarth_MatchesHorizons(string instant, double tilt)
    {
        var rings = _service.SaturnRings(DateTimeOffset.Parse(instant));

        Assert.InRange(rings.TiltDegrees, tilt - TiltTolerance, tilt + TiltTolerance);
    }

    [Fact]
    public void Now_OpeningUntil2032()
    {
        // Horizons, monthly: widest from the Sun 26.731° on 2032-04-01, the nearest sample.
        var rings = _service.SaturnRings(DateTimeOffset.Parse("2026-10-07T00:00:00Z"));

        Assert.Equal(RingTrend.Opening, rings.Trend);
        Assert.InRange(rings.Until, DateTimeOffset.Parse("2032-03-01T00:00:00Z"), DateTimeOffset.Parse("2032-05-01T00:00:00Z"));
        Assert.InRange(rings.TiltAtUntilDegrees, 26.731 - TiltTolerance, 26.731 + TiltTolerance);
    }

    [Theory]
    [InlineData("2032-06-01T00:00:00Z", "2039-01-01T00:00:00Z", "2039-02-01T00:00:00Z")] // Horizons changes sign between these samples
    [InlineData("2025-03-01T00:00:00Z", "2025-05-01T00:00:00Z", "2025-06-01T00:00:00Z")]
    public void AfterTheWidest_ClosingUntilEdgeOn(string instant, string notBefore, string notAfter)
    {
        var rings = _service.SaturnRings(DateTimeOffset.Parse(instant));

        Assert.Equal(RingTrend.Closing, rings.Trend);
        Assert.InRange(rings.Until, DateTimeOffset.Parse(notBefore), DateTimeOffset.Parse(notAfter));
        Assert.Equal(0, rings.TiltAtUntilDegrees);
    }

    [Fact]
    public void MonthToMonth_TheEarthCanReverseIt_ButNotTheTrend()
    {
        // From the Earth the rings close from October to November 2026 (7.36° to 6.45°), yet the trend is opening until 2032.
        var october = _service.SaturnRings(DateTimeOffset.Parse("2026-10-07T00:00:00Z"));
        var november = _service.SaturnRings(DateTimeOffset.Parse("2026-11-06T00:00:00Z"));

        Assert.True(Math.Abs(november.TiltDegrees) < Math.Abs(october.TiltDegrees));
        Assert.Equal(RingTrend.Opening, november.Trend);
    }
}
