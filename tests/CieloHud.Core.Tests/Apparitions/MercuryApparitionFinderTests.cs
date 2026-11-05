namespace CieloHud.Core.Tests.Apparitions;

/// <summary>
/// Drives <see cref="MercuryApparitionFinder"/> with a fake sky. Every day the Sun crosses -6° at 19:00 UTC going down and at 06:00
/// going up, at 0.2° a minute; Mercury moves with it, so its altitude is the day's peak when the Sun is at -6° and falls 0.2° a minute
/// into the night (2° in 10 minutes). Days without a peak keep Mercury below the horizon. The observer's zone is UTC.
/// </summary>
public class MercuryApparitionFinderTests
{
    private static readonly Observer Madrid = new(40.4168, -3.7038, 650);
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly DateOnly Day0 = new(2026, 11, 10);

    private readonly FakeSky _sky = new();

    private MercuryApparitionFinder CreateFinder() => new(_sky, _sky, _sky);

    private static DateTimeOffset At(int day, int hour, int minute = 0) =>
        new DateTimeOffset(Day0.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.Zero).AddDays(day);

    private IReadOnlyList<MercuryWindow> Windows(int fromDay = -1, int toDay = 30) =>
        CreateFinder().FindWindows(Madrid, At(fromDay, 12), At(toDay, 12), Utc);

    private IReadOnlyList<MercuryApparition> Find(DateTimeOffset from, DateTimeOffset to) =>
        CreateFinder().Find(Madrid, from, to, Utc);

    private void Dusk(int day, double peak) => _sky.DuskPeaks[Day0.AddDays(day)] = peak;

    private void Dawn(int day, double peak) => _sky.DawnPeaks[Day0.AddDays(day)] = peak;

    [Fact]
    public void Dusk_WindowFromTheDarkUntilMercuryIsTooLow()
    {
        Dusk(0, 15);

        var window = Assert.Single(Windows());

        Assert.Equal(TwilightPeriod.Dusk, window.Period);
        Assert.Equal(At(0, 19), window.Start.Instant);       // the Sun reaches -6°
        Assert.Equal(At(0, 19), window.Best.Instant);        // Mercury is highest with the sky dark
        Assert.Equal(15, window.Best.Mercury.AltitudeDegrees, 6);
        Assert.Equal(At(0, 19, 25), window.End.Instant);     // 15° - 25 × 0.2° = 10°
        Assert.Equal(TimeSpan.FromMinutes(25), window.Duration);
    }

    [Fact]
    public void Dawn_WindowFromMercuryHighEnoughUntilTheSkyBrightens()
    {
        Dawn(0, 12);

        var window = Assert.Single(Windows());

        Assert.Equal(TwilightPeriod.Dawn, window.Period);
        Assert.Equal(At(0, 5, 50), window.Start.Instant);    // 12° - 10 × 0.2° = 10°
        Assert.Equal(At(0, 6), window.Best.Instant);
        Assert.Equal(At(0, 6), window.End.Instant);          // the Sun rises past -6°
    }

    [Fact]
    public void HighEnoughOnlyInABrightSky_NoWindow()
    {
        Dusk(0, 9.9);   // 10.1° at 18:59, with the Sun at -5.8°

        Assert.Empty(Windows());
    }

    [Fact]
    public void ShortWindowBetweenCoarseSamples_IsFound()
    {
        // The Sun reaches -6° at 19:02: the coarse samples at 19:00 and 19:05 both miss the window, but are near the limits.
        _sky.DuskCrossing = new TimeSpan(19, 2, 0);
        Dusk(0, 10.3);

        var window = Assert.Single(Windows());

        Assert.Equal(At(0, 19, 2), window.Start.Instant);
        Assert.Equal(At(0, 19, 3), window.End.Instant);
    }

    [Theory]
    [InlineData(0.5, true)]
    [InlineData(0.51, false)]
    public void Magnitude_AtMostTheLimit(double magnitude, bool kept)
    {
        Dusk(0, 15);
        _sky.MagnitudeAt = _ => magnitude;

        Assert.Equal(kept ? 1 : 0, Windows().Count);
    }

    [Fact]
    public void Magnitude_IsTakenAtTheBestMoment()
    {
        Dusk(0, 15);
        _sky.MagnitudeAt = t => t == At(0, 19) ? -0.4 : 3;

        Assert.Equal(-0.4, Assert.Single(Windows()).Magnitude);
    }

    [Fact]
    public void DaysInARow_OneSeasonWithItsHighestDay()
    {
        Dusk(0, 11);
        Dusk(1, 13);
        Dusk(2, 12.5);

        var apparition = Assert.Single(Find(At(-5, 12), At(10, 12)));

        Assert.Equal(TwilightPeriod.Dusk, apparition.Period);
        Assert.Equal(At(1, 19), apparition.Best.Best.Instant);
        Assert.Equal(At(0, 19), apparition.First.Best.Instant);
        Assert.Equal(At(2, 19), apparition.Last.Best.Instant);
        Assert.Equal(3, apparition.Days);
    }

    [Fact]
    public void ADayMissing_TwoSeasons()
    {
        Dusk(0, 11);
        Dusk(1, 12);
        Dusk(3, 12);
        Dusk(4, 11);

        var apparitions = Find(At(-5, 12), At(10, 12));

        Assert.Equal(2, apparitions.Count);
        Assert.Equal([2, 2], apparitions.Select(a => a.Days));
    }

    [Fact]
    public void ATooFaintDay_SplitsTheSeason()
    {
        for (var day = 0; day < 5; day++)
            Dusk(day, 12);
        _sky.MagnitudeAt = t => t.Day == At(2, 0).Day ? 1 : 0;

        Assert.Equal([2, 2], Find(At(-5, 12), At(10, 12)).Select(a => a.Days));
    }

    [Fact]
    public void DuskAndDawn_SeparateSeasons()
    {
        Dusk(0, 12);
        Dusk(1, 12);
        Dawn(1, 11);
        Dawn(2, 11);

        var apparitions = Find(At(-5, 12), At(10, 12));

        Assert.Equal(2, apparitions.Count);
        Assert.Contains(apparitions, a => a.Period == TwilightPeriod.Dusk && a.Days == 2);
        Assert.Contains(apparitions, a => a.Period == TwilightPeriod.Dawn && a.Days == 2);
    }

    [Fact]
    public void Season_ComesWholeWhenOnlyItsBestDayIsInTheRange()
    {
        for (var day = 0; day < 7; day++)
            Dusk(day, 10 + day);    // best on day 6
        for (var day = 7; day < 12; day++)
            Dusk(day, 12);

        var apparition = Assert.Single(Find(At(6, 12), At(7, 12)));

        Assert.Equal(At(6, 19), apparition.Best.Best.Instant);
        Assert.Equal(At(0, 19), apparition.First.Best.Instant);
        Assert.Equal(At(11, 19), apparition.Last.Best.Instant);
        Assert.Equal(12, apparition.Days);
    }

    [Fact]
    public void Season_LeftOutWhenItsBestDayIsOutsideTheRange()
    {
        Dusk(0, 11);
        Dusk(1, 14);    // best
        Dusk(2, 11);

        Assert.Empty(Find(At(2, 12), At(5, 12)));     // already past
        Assert.Empty(Find(At(-3, 12), At(1, 12)));    // not yet
    }

    [Fact]
    public void Season_ReturnedDuringItsBestWindow()
    {
        Dusk(0, 14);

        Assert.Single(Find(At(0, 19, 10), At(1, 12)));
    }

    [Fact]
    public void WindowStillOpenAtTheEndOfTheRange_ComesWhole()
    {
        Dusk(0, 15);

        var window = Assert.Single(CreateFinder().FindWindows(Madrid, At(0, 12), At(0, 19, 3), Utc));

        Assert.Equal(At(0, 19, 25), window.End.Instant);
    }

    [Fact]
    public void Result_DoesNotDependOnWhenItIsComputed()
    {
        _sky.DuskCrossing = new TimeSpan(19, 2, 0);
        Dusk(0, 12);

        var a = Assert.Single(CreateFinder().FindWindows(Madrid, At(0, 12), At(1, 12), Utc));
        var b = Assert.Single(CreateFinder().FindWindows(Madrid, At(0, 12).AddSeconds(97), At(1, 12), Utc));

        Assert.Equal(a, b);
    }

    [Fact]
    public void Period_IsToldByLocalTime()
    {
        // 06:00 UTC is 15:00 in Tokyo: a dawn in UTC is a dusk there.
        Dawn(0, 12);
        var tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

        var window = Assert.Single(CreateFinder().FindWindows(Madrid, At(-1, 12), At(2, 12), tokyo));

        Assert.Equal(TwilightPeriod.Dusk, window.Period);
    }

    [Fact]
    public void InvalidCriteria_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MercuryApparitionFinder(_sky, _sky, _sky, new MercuryCriteria { Step = TimeSpan.Zero }));
        Assert.Throws<ArgumentException>(() => new MercuryApparitionFinder(_sky, _sky, _sky,
            new MercuryCriteria { CoarseStep = TimeSpan.FromMinutes(5), Step = TimeSpan.FromMinutes(2) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MercuryApparitionFinder(_sky, _sky, _sky, new MercuryCriteria { CoarseMarginDegrees = -1 }));
    }

    /// <summary>The Sun, Mercury and Mercury's brightness, all moving together (see the class summary).</summary>
    private sealed class FakeSky : ISolarSystemService, ISunService, IMagnitudeService
    {
        private const double DegreesPerMinute = 0.2;

        public Dictionary<DateOnly, double> DuskPeaks { get; } = [];
        public Dictionary<DateOnly, double> DawnPeaks { get; } = [];
        public TimeSpan DuskCrossing { get; set; } = new(19, 0, 0);
        public TimeSpan DawnCrossing { get; set; } = new(6, 0, 0);
        public Func<DateTimeOffset, double> MagnitudeAt { get; set; } = _ => 0;

        /// <summary>Minutes from the Sun's -6° crossing into the night: positive after dusk and before dawn.</summary>
        private double MinutesIntoNight(DateTimeOffset t) => t.UtcDateTime.Hour >= 12
            ? (t.UtcDateTime.TimeOfDay - DuskCrossing).TotalMinutes
            : (DawnCrossing - t.UtcDateTime.TimeOfDay).TotalMinutes;

        public HorizontalPosition Locate(Observer observer, DateTimeOffset instant) =>
            new(180, Math.Clamp(-6 - DegreesPerMinute * MinutesIntoNight(instant), -40, 40));

        public HorizontalPosition Locate(CelestialBody body, Observer observer, DateTimeOffset instant)
        {
            Assert.Equal(CelestialBody.Mercury, body);
            var peaks = instant.UtcDateTime.Hour >= 12 ? DuskPeaks : DawnPeaks;
            if (!peaks.TryGetValue(DateOnly.FromDateTime(instant.UtcDateTime), out var peak))
                return new HorizontalPosition(0, -30);
            var dusk = instant.UtcDateTime.Hour >= 12;
            return new HorizontalPosition(dusk ? 250 : 110, peak - DegreesPerMinute * MinutesIntoNight(instant));
        }

        public double Magnitude(CelestialBody body, DateTimeOffset instant) => MagnitudeAt(instant);
    }
}
