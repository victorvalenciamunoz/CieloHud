namespace CieloHud.Core.Tests.Cards;

/// <summary>
/// Reference values from JPL Horizons (https://ssd.jpl.nasa.gov/api/horizons.api), observer tables queried on 2026-10-06:
/// <list type="bullet">
/// <item>Topocentric, Madrid (CENTER='coord@399', SITE_COORD='-3.7038,40.4168,0.650'), QUANTITIES='10,20,21': lit percentage,
/// distance (delta, AU) and one-way light time (minutes) of the Moon (301) and the planets (199-699).</item>
/// <item>Principal phases: geocentric (CENTER='500@399') ecliptic longitude of the Moon and the Sun (QUANTITIES='31') 10 min either
/// side of each phase, interpolated to the instant their difference is 0°, 90°, 180° or 270°.</item>
/// </list>
/// </summary>
public class AstronomyEngineSolarSystemFactsServiceTests
{
    private const double KmPerAstronomicalUnit = 149_597_870.7;

    // Measured differences: lit fraction ≤ 0.003 percentage points, distance and light time ≤ 0.01 %, principal phases 27-38 s.
    private const double LitPercentTolerance = 0.05;
    private const double DistanceToleranceRatio = 0.001;
    private static readonly TimeSpan QuarterTolerance = TimeSpan.FromMinutes(1);

    private static readonly Observer Madrid = new(latitudeDegrees: 40.4168, longitudeDegrees: -3.7038, altitudeMeters: 650);

    private readonly AstronomyEngineSolarSystemFactsService _service = new();

    [Theory]
    [InlineData("2026-10-06T05:45:00Z", 21.03511, 0.00247077058239, 0.02054877)]
    [InlineData("2026-10-12T19:00:00Z", 4.79208, 0.00265155851890, 0.02205234)]
    [InlineData("2026-10-20T21:00:00Z", 70.43015, 0.00261844838820, 0.02177697)]
    public void Moon_LitFractionDistanceAndLightTime_MatchHorizonsFromMadrid(string instant, double litPercent, double distanceAu, double lightTimeMinutes)
    {
        var moon = _service.Moon(Madrid, DateTimeOffset.Parse(instant));

        Assert.InRange(moon.IlluminatedFraction * 100, litPercent - LitPercentTolerance, litPercent + LitPercentTolerance);
        AssertClose(distanceAu * KmPerAstronomicalUnit, moon.DistanceKm);
        AssertClose(lightTimeMinutes, moon.LightTime.TotalMinutes);
    }

    [Fact]
    public void Moon_LitFraction_IsTopocentric()
    {
        // Horizons from the center of the Earth at the same instant: 21.48799 %. The Moon's parallax is worth half a point.
        var moon = _service.Moon(Madrid, DateTimeOffset.Parse("2026-10-06T05:45:00Z"));

        Assert.True(Math.Abs(moon.IlluminatedFraction * 100 - 21.48799) > 0.4);
    }

    [Theory]
    [InlineData("2026-10-06T05:45:00Z", MoonQuarterKind.NewMoon, "2026-10-10T15:50:05Z")]
    [InlineData("2026-10-12T19:00:00Z", MoonQuarterKind.FirstQuarter, "2026-10-18T16:12:41Z")]
    [InlineData("2026-10-20T21:00:00Z", MoonQuarterKind.FullMoon, "2026-10-26T04:11:48Z")]
    [InlineData("2026-10-27T12:00:00Z", MoonQuarterKind.LastQuarter, "2026-11-01T20:28:27Z")]
    public void Moon_NextPrincipalPhase_MatchesHorizons(string instant, MoonQuarterKind kind, string expectedAt)
    {
        var next = _service.Moon(Madrid, DateTimeOffset.Parse(instant)).Next;

        Assert.Equal(kind, next.Kind);
        var expected = DateTimeOffset.Parse(expectedAt);
        Assert.InRange(next.Instant, expected - QuarterTolerance, expected + QuarterTolerance);
    }

    [Theory]
    [InlineData("2026-10-06T05:45:00Z", MoonPhaseName.WaningCrescent)] // last quarter 2.7 days before, new moon 4.4 days after
    [InlineData("2026-10-18T20:00:00Z", MoonPhaseName.FirstQuarter)] // 4 h after it
    [InlineData("2026-10-20T21:00:00Z", MoonPhaseName.WaxingGibbous)]
    [InlineData("2026-10-25T20:00:00Z", MoonPhaseName.FullMoon)] // the evening before it
    [InlineData("2026-10-27T12:00:00Z", MoonPhaseName.WaningGibbous)] // 1.3 days after it
    public void Moon_PhaseName_OnRealDates(string instant, MoonPhaseName expected)
    {
        Assert.Equal(expected, _service.Moon(Madrid, DateTimeOffset.Parse(instant)).Phase);
    }

    [Fact]
    public void Moon_JustAfterAPrincipalPhase_NextIsTheFollowingOne()
    {
        var moon = _service.Moon(Madrid, DateTimeOffset.Parse("2026-10-26T04:30:00Z"));

        Assert.Equal(MoonPhaseName.FullMoon, moon.Phase);
        Assert.Equal(MoonQuarterKind.LastQuarter, moon.Next.Kind);
    }

    [Theory]
    [InlineData(CelestialBody.Mercury, 1.10259905216253, 9.17003669)]
    [InlineData(CelestialBody.Venus, 0.32076051647568, 2.66768387)]
    [InlineData(CelestialBody.Mars, 1.62889644993041, 13.54711868)]
    [InlineData(CelestialBody.Jupiter, 5.85782376034835, 48.71803465)]
    [InlineData(CelestialBody.Saturn, 8.43467181461624, 70.14902643)]
    public void Planet_DistanceAndLightTime_MatchHorizonsFromMadrid(CelestialBody planet, double distanceAu, double lightTimeMinutes)
    {
        var facts = _service.Planet(planet, Madrid, DateTimeOffset.Parse("2026-10-06T05:45:00Z"));

        Assert.Equal(planet, facts.Body);
        AssertClose(distanceAu * KmPerAstronomicalUnit, facts.DistanceKm);
        AssertClose(lightTimeMinutes, facts.LightTime.TotalMinutes);
    }

    [Fact]
    public void Planet_Moon_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.Planet(CelestialBody.Moon, Madrid, DateTimeOffset.UtcNow));
    }

    private static void AssertClose(double expected, double actual) =>
        Assert.InRange(actual, expected * (1 - DistanceToleranceRatio), expected * (1 + DistanceToleranceRatio));
}
