namespace CieloHud.Core.Tests.Apparitions;

/// <summary>
/// <see cref="MercuryApparitionFinder"/> with the real ephemeris, seen from Madrid, October 2026 to October 2027.
/// Reference values come from JPL Horizons (observer table, CENTER='coord@399', SITE_COORD='-3.7038,40.4168,0.650', STEP_SIZE='1 m';
/// Mercury (199) with QUANTITIES='4,9' and APPARENT='REFRACTED', the Sun (10) with QUANTITIES='4' and APPARENT='AIRLESS'), with the
/// same criterion applied to its data. Queried on 2026-10-06. The windows agree to the minute on every day of the November, February
/// and May seasons, and the positions within 0.0015° over every twilight minute (docs/STATUS.md). Horizons' magnitudes use a newer
/// model and come out up to 0.13 fainter, which moves the last day of May 2027 (0.44 here, 0.51 there).
/// </summary>
public class MercuryApparitionFinderIntegrationTests
{
    private const double AltitudeToleranceDegrees = 0.05;
    private const double MagnitudeTolerance = 0.15;

    private static readonly Observer Madrid = new(latitudeDegrees: 40.4168, longitudeDegrees: -3.7038, altitudeMeters: 650);
    private static readonly TimeZoneInfo MadridZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");
    private static readonly DateTimeOffset From = new(2026, 10, 6, 10, 0, 0, TimeSpan.Zero);

    private static readonly MercuryApparitionFinder Finder = new(
        new AstronomyEngineSolarSystemService(), new AstronomyEngineSunService(), new AstronomyEngineMagnitudeService());

    private static readonly Lazy<IReadOnlyList<MercuryApparition>> Year =
        new(() => Finder.Find(Madrid, From, From.AddDays(365), MadridZone));

    private static readonly Lazy<IReadOnlyList<MercuryWindow>> November =
        new(() => Finder.FindWindows(Madrid, new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 12, 5, 12, 0, 0, TimeSpan.Zero), MadridZone));

    private static DateOnly LocalDay(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, MadridZone).DateTime);

    [Fact]
    public void Year_HasThreeSeasons()
    {
        Assert.Equal(
            [TwilightPeriod.Dawn, TwilightPeriod.Dusk, TwilightPeriod.Dusk],
            Year.Value.Select(a => a.Period));
    }

    // Best moment (UTC); first and last local days; Horizons' altitude and azimuth at the best moment.
    [Theory]
    [InlineData("2026-11-20T06:37Z", "2026-11-14", "2026-11-28", 15, 12.390, 117.848)]
    [InlineData("2027-02-04T18:05Z", "2027-01-31", "2027-02-07", 8, 11.083, 247.452)]
    [InlineData("2027-05-26T20:06Z", "2027-05-15", "2027-05-28", 14, 13.391, 292.301)]
    public void Year_SeasonsMatchJplHorizons(string best, string first, string last, int days, double altitude, double azimuth)
    {
        var apparition = Assert.Single(Year.Value, a => a.Best.Best.Instant == DateTimeOffset.Parse(best));

        Assert.Equal(DateOnly.Parse(first), LocalDay(apparition.First.Best.Instant));
        Assert.Equal(DateOnly.Parse(last), LocalDay(apparition.Last.Best.Instant));
        Assert.Equal(days, apparition.Days);
        Assert.InRange(apparition.Best.Best.Mercury.AltitudeDegrees, altitude - AltitudeToleranceDegrees, altitude + AltitudeToleranceDegrees);
        Assert.InRange(apparition.Best.Best.Mercury.AzimuthDegrees, azimuth - AltitudeToleranceDegrees, azimuth + AltitudeToleranceDegrees);
    }

    // Each day of the November 2026 dawn season: window start and end (UTC; the best moment is the end, as the Sun reaches -6°),
    // and Horizons' altitude and magnitude then.
    [Theory]
    [InlineData("2026-11-14T06:30Z", "2026-11-14T06:30Z", 10.098, 0.20)]
    [InlineData("2026-11-15T06:27Z", "2026-11-15T06:31Z", 10.815, 0.02)]
    [InlineData("2026-11-16T06:25Z", "2026-11-16T06:32Z", 11.368, -0.12)]
    [InlineData("2026-11-17T06:23Z", "2026-11-17T06:33Z", 11.770, -0.23)]
    [InlineData("2026-11-18T06:23Z", "2026-11-18T06:34Z", 12.036, -0.31)]
    [InlineData("2026-11-19T06:23Z", "2026-11-19T06:35Z", 12.183, -0.39)]
    [InlineData("2026-11-20T06:23Z", "2026-11-20T06:37Z", 12.390, -0.45)]
    [InlineData("2026-11-21T06:25Z", "2026-11-21T06:38Z", 12.337, -0.50)]
    [InlineData("2026-11-22T06:26Z", "2026-11-22T06:39Z", 12.204, -0.54)]
    [InlineData("2026-11-23T06:28Z", "2026-11-23T06:40Z", 12.003, -0.57)]
    [InlineData("2026-11-24T06:31Z", "2026-11-24T06:41Z", 11.743, -0.60)]
    [InlineData("2026-11-25T06:34Z", "2026-11-25T06:42Z", 11.433, -0.62)]
    [InlineData("2026-11-26T06:37Z", "2026-11-26T06:43Z", 11.081, -0.64)]
    [InlineData("2026-11-27T06:40Z", "2026-11-27T06:44Z", 10.695, -0.65)]
    [InlineData("2026-11-28T06:44Z", "2026-11-28T06:45Z", 10.280, -0.67)]
    public void November_EveryDayMatchesJplHorizons(string start, string end, double altitude, double magnitude)
    {
        var window = Assert.Single(November.Value, w => w.Start.Instant == DateTimeOffset.Parse(start));

        Assert.Equal(TwilightPeriod.Dawn, window.Period);
        Assert.Equal(DateTimeOffset.Parse(end), window.End.Instant);
        Assert.Equal(window.End, window.Best);
        Assert.InRange(window.Best.Mercury.AltitudeDegrees, altitude - AltitudeToleranceDegrees, altitude + AltitudeToleranceDegrees);
        Assert.InRange(window.Magnitude, magnitude - MagnitudeTolerance, magnitude + MagnitudeTolerance);
    }

    [Fact]
    public void November_OnlyTheFifteenDays()
    {
        Assert.Equal(15, November.Value.Count);
    }

    [Fact]
    public void October2026_GreatestElongationButTooLow()
    {
        // 25° from the Sun on 12 Oct, but the ecliptic lies flat at dusk: 3.39° up when the Sun reaches -6°, at 18:07 UTC (Horizons 3.391°).
        var october = Finder.FindWindows(Madrid, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), MadridZone);
        var mercury = new AstronomyEngineSolarSystemService().Locate(CelestialBody.Mercury, Madrid, new DateTimeOffset(2026, 10, 12, 18, 7, 0, TimeSpan.Zero));

        Assert.Empty(october);
        Assert.InRange(mercury.AltitudeDegrees, 3.391 - AltitudeToleranceDegrees, 3.391 + AltitudeToleranceDegrees);
    }

    [Fact]
    public void Year_EveryDayMeetsTheCriteria()
    {
        var criteria = Finder.Criteria;
        var windows = Finder.FindWindows(Madrid, From, From.AddDays(365), MadridZone);

        Assert.Equal(Year.Value.Sum(a => a.Days), windows.Count);
        foreach (var w in windows)
        {
            Assert.True(w.Magnitude <= criteria.MaxMagnitude);
            foreach (var point in new[] { w.Start, w.Best, w.End })
            {
                Assert.True(point.Mercury.AltitudeDegrees >= criteria.MinAltitudeDegrees);
                Assert.True(point.SunAltitudeDegrees <= criteria.MaxSunAltitudeDegrees);
            }
        }
    }
}
