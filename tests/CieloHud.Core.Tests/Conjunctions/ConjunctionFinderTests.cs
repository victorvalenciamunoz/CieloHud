namespace CieloHud.Core.Tests.Conjunctions;

/// <summary>
/// Drives <see cref="ConjunctionFinder"/> with a fake sky: dark from 19:00 to 07:00 UTC, the Moon due south at a chosen
/// altitude and each planet straight above it, so the separation is exactly the altitude difference.
/// Times are UTC and the observer's zone is UTC unless a test says otherwise.
/// </summary>
public class ConjunctionFinderTests
{
    private static readonly Observer Madrid = new(40.4168, -3.7038, 650);
    private static readonly DateTimeOffset Noon = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private readonly FakeSky _sky = new();
    private readonly FakeSun _sun = new();

    private ConjunctionFinder CreateFinder(ConjunctionCriteria? criteria = null) => new(_sky, _sun, criteria);

    private IReadOnlyList<MoonPlanetConjunction> FindNight(TimeZoneInfo? zone = null) =>
        CreateFinder().Find(Madrid, Noon, Noon.AddDays(1), zone ?? Utc);

    private static DateTimeOffset At(int hour, int minute = 0, int day = 0) =>
        new DateTimeOffset(2026, 10, 5, hour, minute, 0, TimeSpan.Zero).AddDays(day);

    /// <summary>Separation closing at 0.5° an hour, like the Moon, down to <paramref name="minimum"/> at <paramref name="closest"/>.</summary>
    private static Func<DateTimeOffset, double> Approach(DateTimeOffset closest, double minimum, double degreesPerHour = 0.5) =>
        t => minimum + degreesPerHour * Math.Abs((t - closest).TotalHours);

    [Fact]
    public void CloseApproachAtNight_WindowAndBestMoment()
    {
        _sky.Planet(CelestialBody.Jupiter, Approach(At(22), 1.1));

        var conjunction = Assert.Single(FindNight());

        Assert.Equal(CelestialBody.Jupiter, conjunction.Planet);
        Assert.Equal(At(19), conjunction.Start.Instant);      // separation 2.6° at dusk: the window opens with the dark
        Assert.Equal(At(22), conjunction.Best.Instant);
        Assert.Equal(1.1, conjunction.Best.SeparationDegrees, 6);
        Assert.Equal(conjunction.Best, conjunction.Closest);
        Assert.Equal(At(5, 45, day: 1), conjunction.End.Instant); // 4.975° at 5:45, 5.017° at 5:50
        Assert.Equal(40, conjunction.Best.Moon.AltitudeDegrees, 6);
    }

    [Fact]
    public void Daytime_NothingFound()
    {
        _sun.AltitudeAt = _ => 20;
        _sky.Planet(CelestialBody.Jupiter, Approach(At(22), 1));

        Assert.Empty(FindNight());
    }

    [Theory]
    [InlineData(-5.9)]
    [InlineData(-3)]
    public void CivilTwilightTooBright_NothingFound(double sunAltitude)
    {
        _sun.AltitudeAt = _ => sunAltitude;
        _sky.Planet(CelestialBody.Jupiter, Approach(At(22), 1));

        Assert.Empty(FindNight());
    }

    [Fact]
    public void FarApart_NothingFound()
    {
        _sky.Planet(CelestialBody.Jupiter, _ => 5.2);

        Assert.Empty(FindNight());
    }

    [Fact]
    public void MoonTooLow_NothingFound()
    {
        _sky.MoonAltitude = _ => 9;
        _sky.Planet(CelestialBody.Jupiter, _ => 0.5); // the planet itself at 9.5°

        Assert.Empty(FindNight());
    }

    [Fact]
    public void PlanetTooLow_NothingFound()
    {
        // The planet sits below the Moon: Moon at 12°, planet at 8°.
        _sky.MoonAltitude = _ => 12;
        _sky.Planet(CelestialBody.Jupiter, _ => -4);

        Assert.Empty(FindNight());
    }

    [Fact]
    public void ClosestLowOverTheHorizon_BestIsWhereBothAreComfortablyHigh()
    {
        // Setting together: 60° at 19:00, down 10° an hour, 20° at 23:00; the closest approach comes later, at 15°.
        _sky.MoonAltitude = t => 60 - 10 * (t - At(19)).TotalHours;
        _sky.Planet(CelestialBody.Jupiter, Approach(At(23, 30), 1));

        var conjunction = Assert.Single(FindNight());

        Assert.Equal(At(23, 30), conjunction.Closest.Instant);
        Assert.Equal(At(23), conjunction.Best.Instant); // Moon at 20°, separation 1.25°
        Assert.Equal(1.25, conjunction.Best.SeparationDegrees, 6);
        Assert.Equal(At(0, day: 1), conjunction.End.Instant); // the Moon at 10°
    }

    [Fact]
    public void NeverComfortablyHigh_BestIsTheClosest()
    {
        _sky.MoonAltitude = _ => 15;
        _sky.Planet(CelestialBody.Jupiter, Approach(At(23), 1));

        var conjunction = Assert.Single(FindNight());

        Assert.Equal(At(23), conjunction.Best.Instant);
    }

    [Fact]
    public void WindowAcrossMidnight_BestIsInTheEvening()
    {
        // Closest at 3:00, but the evening part qualifies too: best is the closest before midnight.
        _sky.Planet(CelestialBody.Jupiter, Approach(At(3, day: 1), 1));

        var conjunction = Assert.Single(FindNight());

        Assert.Equal(At(3, day: 1), conjunction.Closest.Instant);
        Assert.Equal(At(23, 55), conjunction.Best.Instant);
    }

    [Fact]
    public void Evening_IsLocalNotUtc()
    {
        // In UTC+2 local midnight is 22:00 UTC: the evening part ends there.
        var plusTwo = TimeZoneInfo.CreateCustomTimeZone("UTC+2", TimeSpan.FromHours(2), "UTC+2", "UTC+2");
        _sky.Planet(CelestialBody.Jupiter, Approach(At(3, day: 1), 1));

        var conjunction = Assert.Single(FindNight(plusTwo));

        Assert.Equal(At(21, 55), conjunction.Best.Instant);
    }

    [Fact]
    public void OnlyAfterMidnight_BestIsTheClosest()
    {
        _sky.MoonAltitude = t => t < At(1, day: 1) ? 5 : 40; // the Moon rises past 10° at 1:00
        _sky.Planet(CelestialBody.Jupiter, Approach(At(4, day: 1), 1));

        var conjunction = Assert.Single(FindNight());

        Assert.Equal(At(1, day: 1), conjunction.Start.Instant);
        Assert.Equal(At(4, day: 1), conjunction.Best.Instant);
    }

    [Fact]
    public void WindowOpenAtFrom_IsCutThere()
    {
        _sky.Planet(CelestialBody.Jupiter, Approach(At(22), 1));
        var from = At(21, 2);

        var conjunction = Assert.Single(CreateFinder().Find(Madrid, from, Noon.AddDays(1), Utc));

        Assert.Equal(from, conjunction.Start.Instant);
    }

    [Fact]
    public void WindowOpenAtTo_IsReturnedWhole()
    {
        _sky.Planet(CelestialBody.Jupiter, Approach(At(22), 1));

        var conjunction = Assert.Single(CreateFinder().Find(Madrid, Noon, At(20), Utc));

        Assert.Equal(At(19), conjunction.Start.Instant);
        Assert.Equal(At(22), conjunction.Best.Instant);
        Assert.True(conjunction.End.Instant > At(5, day: 1));
    }

    [Fact]
    public void WindowStartingAfterTo_IsNotReturned()
    {
        _sky.Planet(CelestialBody.Jupiter, Approach(At(22), 1));

        Assert.Empty(CreateFinder().Find(Madrid, Noon, At(18), Utc));
    }

    [Fact]
    public void DawnAndDuskOfOneApproach_KeepsTheCloser()
    {
        // Closest at midday: 3.04° at the last dark sample of dawn (6:55), 3.0° at dusk (19:00) (the Moon moves 0.5° an hour).
        _sky.Planet(CelestialBody.Jupiter, Approach(At(13, day: 1), 0));
        var conjunctions = CreateFinder().Find(Madrid, Noon, Noon.AddDays(2), Utc);

        var conjunction = Assert.Single(conjunctions);
        Assert.Equal(At(19, day: 1), conjunction.Best.Instant);
        Assert.Equal(3.0, conjunction.Best.SeparationDegrees, 6);
    }

    [Fact]
    public void TwoPlanetsTheSameNight_BothFoundInOrder()
    {
        _sky.Planet(CelestialBody.Mars, Approach(At(23), 2));
        _sky.Planet(CelestialBody.Jupiter, Approach(At(21), 3));

        var conjunctions = FindNight();

        Assert.Equal([CelestialBody.Jupiter, CelestialBody.Mars], conjunctions.Select(c => c.Planet));
    }

    [Fact]
    public void Mercury_NotLookedAtByDefault()
    {
        _sky.Planet(CelestialBody.Mercury, Approach(At(22), 1));

        Assert.Empty(FindNight());
        Assert.Single(CreateFinder(new ConjunctionCriteria { Planets = [CelestialBody.Mercury] })
            .Find(Madrid, Noon, Noon.AddDays(1), Utc));
    }

    [Fact]
    public void DefaultCriteria_AsAgreed()
    {
        var criteria = ConjunctionCriteria.Default;

        Assert.Equal(5, criteria.MaxSeparationDegrees);
        Assert.Equal(10, criteria.MinAltitudeDegrees);
        Assert.Equal(-6, criteria.MaxSunAltitudeDegrees);
        Assert.Equal([CelestialBody.Venus, CelestialBody.Mars, CelestialBody.Jupiter, CelestialBody.Saturn], criteria.Planets);
    }

    [Fact]
    public void InvalidCriteria_Throw()
    {
        Assert.ThrowsAny<ArgumentException>(() => CreateFinder(new ConjunctionCriteria { MaxSeparationDegrees = 0 }));
        Assert.ThrowsAny<ArgumentException>(() => CreateFinder(new ConjunctionCriteria { ComfortableAltitudeDegrees = 5 }));
        Assert.ThrowsAny<ArgumentException>(() => CreateFinder(new ConjunctionCriteria { Step = TimeSpan.Zero }));
        Assert.ThrowsAny<ArgumentException>(() => CreateFinder(new ConjunctionCriteria { Planets = [CelestialBody.Moon] }));
    }

    [Fact]
    public void NullTimeZone_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CreateFinder().Find(Madrid, Noon, Noon.AddDays(1), null!));
    }

    /// <summary>The Moon due south; each planet straight above (or below) it by its separation, so the separation is exact.</summary>
    private sealed class FakeSky : ISolarSystemService
    {
        private readonly Dictionary<CelestialBody, Func<DateTimeOffset, double>> _planets = [];

        public Func<DateTimeOffset, double> MoonAltitude { get; set; } = _ => 40;

        public void Planet(CelestialBody body, Func<DateTimeOffset, double> separation) => _planets[body] = separation;

        public HorizontalPosition Locate(CelestialBody body, Observer observer, DateTimeOffset instant)
        {
            if (body == CelestialBody.Moon)
                return new HorizontalPosition(180, MoonAltitude(instant));
            return _planets.TryGetValue(body, out var separation)
                ? new HorizontalPosition(180, MoonAltitude(instant) + separation(instant))
                : new HorizontalPosition(0, -30);
        }
    }

    /// <summary>Dark (-20°) from 19:00 to 07:00 UTC, daylight (+20°) otherwise.</summary>
    private sealed class FakeSun : ISunService
    {
        public Func<DateTimeOffset, double> AltitudeAt { get; set; } =
            t => t.UtcDateTime.Hour is >= 19 or < 7 ? -20 : 20;

        public HorizontalPosition Locate(Observer observer, DateTimeOffset instant) => new(180, AltitudeAt(instant));
    }
}
