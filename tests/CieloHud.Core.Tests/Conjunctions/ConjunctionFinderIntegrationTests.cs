namespace CieloHud.Core.Tests.Conjunctions;

/// <summary>
/// <see cref="ConjunctionFinder"/> with the real ephemeris, seen from Madrid, autumn and winter 2026-27.
/// Reference separations come from JPL Horizons (observer table, CENTER='coord@399', SITE_COORD='-3.7038,40.4168,0.650',
/// QUANTITIES='4', APPARENT='REFRACTED', STEP_SIZE='5 m'): the angle between the refracted azimuth/elevation of the Moon (301)
/// and the planet, computed like the finder does. Queried on 2026-10-05. Over every sample of the seven windows, the
/// separation differs from Horizons by at most 0.0044°; the windows and best moments agree to the sample (docs/STATUS.md).
/// </summary>
public class ConjunctionFinderIntegrationTests
{
    private const double SeparationToleranceDegrees = 0.05;

    private static readonly Observer Madrid = new(latitudeDegrees: 40.4168, longitudeDegrees: -3.7038, altitudeMeters: 650);
    private static readonly TimeZoneInfo MadridZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");
    private static readonly DateTimeOffset From = new(2026, 10, 5, 10, 0, 0, TimeSpan.Zero);

    private static readonly ConjunctionFinder Finder = new(new AstronomyEngineSolarSystemService(), new AstronomyEngineSunService());

    private static readonly Lazy<IReadOnlyList<MoonPlanetConjunction>> Season =
        new(() => Finder.Find(Madrid, From, From.AddDays(120), MadridZone));

    // Best moment (UTC), window start and end (UTC), and Horizons' separation at the best moment.
    [Theory]
    [InlineData(CelestialBody.Jupiter, "2026-10-06T05:45Z", "2026-10-06T02:50Z", "2026-10-06T05:45Z", 1.997)]
    [InlineData(CelestialBody.Mars, "2026-11-02T06:15Z", "2026-11-02T03:50Z", "2026-11-02T06:15Z", 4.093)]
    [InlineData(CelestialBody.Jupiter, "2026-11-03T02:30Z", "2026-11-03T01:35Z", "2026-11-03T06:15Z", 3.064)]
    [InlineData(CelestialBody.Venus, "2026-11-07T06:20Z", "2026-11-07T06:15Z", "2026-11-07T06:20Z", 1.987)]
    [InlineData(CelestialBody.Jupiter, "2026-11-30T06:45Z", "2026-11-29T23:45Z", "2026-11-30T06:45Z", 1.863)]
    [InlineData(CelestialBody.Jupiter, "2026-12-27T22:20Z", "2026-12-27T22:20Z", "2026-12-27T22:45Z", 4.746)]
    [InlineData(CelestialBody.Jupiter, "2027-01-23T22:00Z", "2027-01-23T19:55Z", "2027-01-24T07:00Z", 1.580)]
    public void Season_MatchesJplHorizons(CelestialBody planet, string best, string start, string end, double horizonsSeparation)
    {
        var conjunction = Assert.Single(Season.Value, c => c.Planet == planet && c.Best.Instant == DateTimeOffset.Parse(best));

        Assert.Equal(DateTimeOffset.Parse(start), conjunction.Start.Instant);
        Assert.Equal(DateTimeOffset.Parse(end), conjunction.End.Instant);
        Assert.InRange(conjunction.Best.SeparationDegrees,
            horizonsSeparation - SeparationToleranceDegrees, horizonsSeparation + SeparationToleranceDegrees);
    }

    [Fact]
    public void Season_HasExactlyTheSevenNights()
    {
        Assert.Equal(7, Season.Value.Count);
    }

    [Fact]
    public void Season_EveryConjunctionMeetsTheCriteriaAtItsKeyMoments()
    {
        var criteria = Finder.Criteria;
        var sun = new AstronomyEngineSunService();
        foreach (var c in Season.Value)
        {
            foreach (var point in new[] { c.Start, c.Best, c.Closest, c.End })
            {
                Assert.True(point.SeparationDegrees <= criteria.MaxSeparationDegrees);
                Assert.True(point.LowerAltitudeDegrees >= criteria.MinAltitudeDegrees);
                Assert.True(sun.Locate(Madrid, point.Instant).AltitudeDegrees <= criteria.MaxSunAltitudeDegrees);
            }
            Assert.True(c.Start.Instant <= c.Best.Instant && c.Best.Instant <= c.End.Instant);
            Assert.True(c.Closest.SeparationDegrees <= c.Best.SeparationDegrees);
        }
    }

    [Fact]
    public void Season_BestMomentPrefersHeightOverTheLowClosestApproach()
    {
        // 3 Nov (UTC+1): closest at 2:35 local (2.60°) with the Moon at 10°; best at 3:30 local (3.06°), both above 20°.
        var november3 = Assert.Single(Season.Value, c => c.Best.Instant == DateTimeOffset.Parse("2026-11-03T02:30Z"));

        Assert.Equal(DateTimeOffset.Parse("2026-11-03T01:35Z"), november3.Closest.Instant);
        Assert.True(november3.Closest.LowerAltitudeDegrees < 11);
        Assert.True(november3.Best.LowerAltitudeDegrees >= 20);
    }
}
