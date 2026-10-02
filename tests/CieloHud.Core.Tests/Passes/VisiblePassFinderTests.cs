using CieloHud.Core.SolarSystem;
using CieloHud.Core.Tests.Satellites;

namespace CieloHud.Core.Tests.Passes;

/// <summary>
/// Drives <see cref="VisiblePassFinder"/> with fake services: one synthetic 10-minute pass peaking at 40° after 5 minutes,
/// a Sun at a fixed altitude, and illumination that can switch at a chosen instant.
/// </summary>
public class VisiblePassFinderTests
{
    private static readonly Observer Madrid = new(40.4168, -3.7038, 650);
    private static readonly DateTimeOffset T0 = new(2026, 10, 2, 20, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan HalfPass = TimeSpan.FromMinutes(5);

    private readonly FakePassPredictor _passes = new(SyntheticPass(peakAltitude: 40));
    private readonly FakeSun _sun = new(altitude: -20);
    private readonly FakeIllumination _illumination = new();

    private VisiblePassFinder CreateFinder(VisibilityCriteria? criteria = null) =>
        new(_passes, new SyntheticSatellite(), _sun, _illumination, criteria);

    [Fact]
    public void NightAndLit_WholePassIsVisible()
    {
        var visible = CreateFinder().Find(TleTests.Iss, Madrid, T0, T0.AddHours(1));

        var pass = Assert.Single(visible);
        Assert.Equal(pass.Pass.Start, pass.VisibleStart);
        Assert.Equal(pass.Pass.Max, pass.VisibleMax);
        Assert.Equal(pass.Pass.End, pass.VisibleEnd);
        Assert.False(pass.EndsInShadow);
        Assert.False(pass.StartsFromShadow);
    }

    [Theory]
    [InlineData(10)]  // day
    [InlineData(-3)]  // civil twilight, too bright
    [InlineData(-5.9)]
    public void SkyNotDarkEnough_NothingVisible(double sunAltitude)
    {
        _sun.Altitude = sunAltitude;

        Assert.Empty(CreateFinder().Find(TleTests.Iss, Madrid, T0, T0.AddHours(1)));
    }

    [Fact]
    public void SunJustBelowThreshold_IsVisible()
    {
        _sun.Altitude = -6.1;

        Assert.Single(CreateFinder().Find(TleTests.Iss, Madrid, T0, T0.AddHours(1)));
    }

    [Fact]
    public void EntersShadowMidPass_VisibleEndIsShadowEntry()
    {
        var shadowEntry = T0.AddMinutes(6);
        _illumination.DarkFrom = shadowEntry;

        var pass = Assert.Single(CreateFinder().Find(TleTests.Iss, Madrid, T0, T0.AddHours(1)));

        Assert.Equal(pass.Pass.Start, pass.VisibleStart);
        Assert.Equal(pass.Pass.Max, pass.VisibleMax);
        Assert.InRange(pass.VisibleEnd.Instant, shadowEntry - TimeSpan.FromSeconds(10), shadowEntry);
        Assert.True(pass.EndsInShadow);
        Assert.True(pass.VisibleEnd.Position.AltitudeDegrees > 30, "still high when it vanishes");
    }

    [Fact]
    public void LeavesShadowMidPass_VisibleStartIsShadowExit_MaxIsFirstVisibleSample()
    {
        var shadowExit = T0.AddMinutes(7);
        _illumination.DarkUntil = shadowExit;

        var pass = Assert.Single(CreateFinder().Find(TleTests.Iss, Madrid, T0, T0.AddHours(1)));

        Assert.InRange(pass.VisibleStart.Instant, shadowExit, shadowExit + TimeSpan.FromSeconds(10));
        Assert.True(pass.StartsFromShadow);
        Assert.Equal(pass.VisibleStart, pass.VisibleMax); // already descending when it appears
        Assert.Equal(pass.Pass.End, pass.VisibleEnd);
    }

    [Fact]
    public void AlwaysInShadow_NothingVisible()
    {
        _illumination.DarkUntil = T0.AddDays(1);

        Assert.Empty(CreateFinder().Find(TleTests.Iss, Madrid, T0, T0.AddHours(1)));
    }

    [Fact]
    public void LowPass_BelowMinPeak_NotVisible_UnlessCriteriaRelaxed()
    {
        var lowPasses = new FakePassPredictor(SyntheticPass(peakAltitude: 8));
        var strict = new VisiblePassFinder(lowPasses, new SyntheticSatellite(), _sun, _illumination);
        var relaxed = new VisiblePassFinder(lowPasses, new SyntheticSatellite(), _sun, _illumination, new VisibilityCriteria { MinPeakAltitudeDegrees = 5 });

        Assert.Empty(strict.Find(TleTests.Iss, Madrid, T0, T0.AddHours(1)));
        Assert.Single(relaxed.Find(TleTests.Iss, Madrid, T0, T0.AddHours(1)));
    }

    [Fact]
    public void ShadowEntryBeforeReachingMinPeak_NotVisible()
    {
        // Altitude after 30 s is about 4°; the satellite goes dark before it gets high enough to count.
        _illumination.DarkFrom = T0.AddSeconds(30);

        Assert.Empty(CreateFinder().Find(TleTests.Iss, Madrid, T0, T0.AddHours(1)));
    }

    // Synthetic pass: altitude = peak · (1 − ((t − tMax) / 5 min)²), azimuth from 300° to 60° through north.
    private static SatellitePass SyntheticPass(double peakAltitude)
    {
        var sat = new SyntheticSatellite(peakAltitude);
        PassPoint At(DateTimeOffset t) => new(t, sat.Locate(default, Madrid, t));
        return new SatellitePass(At(T0), At(T0 + HalfPass), At(T0 + HalfPass + HalfPass));
    }

    private sealed class SyntheticSatellite(double peakAltitude = 40) : ISatelliteService
    {
        public HorizontalPosition Locate(Tle tle, Observer observer, DateTimeOffset instant)
        {
            var x = (instant - (T0 + HalfPass)) / HalfPass; // -1 at start, 0 at max, +1 at end
            var altitude = Math.Max(-90, peakAltitude * (1 - x * x));
            var azimuth = 300 + 60 * (x + 1);
            return new HorizontalPosition(azimuth, altitude, 1000);
        }
    }

    private sealed class FakePassPredictor(params SatellitePass[] passes) : ISatellitePassPredictor
    {
        public IReadOnlyList<SatellitePass> Predict(Tle tle, Observer observer, DateTimeOffset from, DateTimeOffset to) => passes;
    }

    private sealed class FakeSun(double altitude) : ISunService
    {
        public double Altitude { get; set; } = altitude;
        public HorizontalPosition Locate(Observer observer, DateTimeOffset instant) => new(180, Altitude);
    }

    private sealed class FakeIllumination : ISatelliteIlluminationService
    {
        public DateTimeOffset? DarkFrom { get; set; }
        public DateTimeOffset? DarkUntil { get; set; }

        public bool IsSunlit(Tle tle, DateTimeOffset instant) =>
            !(DarkFrom is { } from && instant >= from) && !(DarkUntil is { } until && instant < until);
    }
}
