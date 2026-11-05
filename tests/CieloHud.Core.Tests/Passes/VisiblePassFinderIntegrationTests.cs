using CieloHud.Core.SolarSystem;
using CieloHud.Core.Tests.Satellites;

namespace CieloHud.Core.Tests.Passes;

/// <summary>
/// Real services end to end with the fixed ISS TLE. External validation against Heavens-Above is manual (docs/STATUS.md);
/// here we check internal consistency of what the finder returns.
/// </summary>
public class VisiblePassFinderIntegrationTests
{
    private static readonly Observer Madrid = new(40.4168, -3.7038, 650);
    private readonly AstronomyEngineSunService _sun = new();
    private readonly Sgp4SatelliteIlluminationService _illumination = new();

    private VisiblePassFinder CreateFinder(VisibilityCriteria? criteria = null) =>
        new(new Sgp4SatellitePassPredictor(), new Sgp4SatelliteService(), _sun, _illumination, criteria);

    [Fact]
    public void EarlyOctober2026_DaytimeClusters_NothingVisibleOverMadrid()
    {
        // All passes from Oct 2 to Oct 4 happen by day or graze the horizon (see STATUS.md, phase 2 step 3).
        var visible = CreateFinder().Find(TleTests.Iss, Madrid, DateTimeOffset.Parse("2026-10-02T12:00:00Z"), DateTimeOffset.Parse("2026-10-04T20:00:00Z"));

        Assert.Empty(visible);
    }

    [Fact]
    public void EarlyOctober2026_RelaxedPeak_FindsTheGrazingDuskPass()
    {
        // Oct 2 19:30 UTC: peak 0.9°, Sun at -19°, ISS lit over the Atlantic.
        var relaxed = new VisibilityCriteria { MinPeakAltitudeDegrees = 0 };

        var visible = CreateFinder(relaxed).Find(TleTests.Iss, Madrid, DateTimeOffset.Parse("2026-10-02T19:00:00Z"), DateTimeOffset.Parse("2026-10-02T20:00:00Z"));

        var pass = Assert.Single(visible);
        Assert.Equal(pass.Pass.Start, pass.VisibleStart);
        Assert.Equal(pass.Pass.End, pass.VisibleEnd);
        Assert.False(pass.EndsInShadow);
        Assert.False(pass.StartsFromShadow);
    }

    [Fact]
    public void TwoWeeks_VisiblePassesSatisfyAllCriteria()
    {
        var from = DateTimeOffset.Parse("2026-10-02T12:00:00Z");
        var to = DateTimeOffset.Parse("2026-10-16T12:00:00Z");

        var visible = CreateFinder().Find(TleTests.Iss, Madrid, from, to);

        Assert.NotEmpty(visible);
        foreach (var pass in visible)
        {
            Assert.True(pass.VisibleMax.Position.AltitudeDegrees >= 10);
            Assert.True(pass.VisibleStart.Instant >= pass.Pass.Start.Instant && pass.VisibleEnd.Instant <= pass.Pass.End.Instant);
            Assert.True(pass.VisibleStart.Instant <= pass.VisibleMax.Instant && pass.VisibleMax.Instant <= pass.VisibleEnd.Instant);
            // Dark when it appears; it may brighten a little afterwards (dawn passes), never past -4° within a pass.
            Assert.True(_sun.Locate(Madrid, pass.VisibleStart.Instant).AltitudeDegrees <= -6, $"sky not dark at {pass.VisibleStart.Instant:u}");
            Assert.True(_sun.Locate(Madrid, pass.VisibleEnd.Instant).AltitudeDegrees <= -4, $"sky far too bright at {pass.VisibleEnd.Instant:u}");
            foreach (var point in new[] { pass.VisibleStart, pass.VisibleMax, pass.VisibleEnd })
                Assert.True(_illumination.IsSunlit(TleTests.Iss, point.Instant), $"ISS in shadow at {point.Instant:u}");
            // The flags must agree with the geometry.
            Assert.Equal(pass.VisibleEnd.Instant < pass.Pass.End.Instant, pass.EndsInShadow);
            Assert.Equal(pass.VisibleStart.Instant > pass.Pass.Start.Instant, pass.StartsFromShadow);
            if (pass.EndsInShadow)
                Assert.False(_illumination.IsSunlit(TleTests.Iss, pass.VisibleEnd.Instant + TimeSpan.FromSeconds(10)), "a pass that ends in shadow must be dark right after its visible end");
            if (pass.StartsFromShadow)
                Assert.False(_illumination.IsSunlit(TleTests.Iss, pass.VisibleStart.Instant - TimeSpan.FromSeconds(10)), "a pass that starts from shadow must be dark right before its visible start");
        }
    }
}
