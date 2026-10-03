namespace CieloHud.Core.Tests.Guidance;

public class SkyIdentifierTests
{
    private static SkyCandidate C(string name, double azimuth, double altitude) => new(name, new HorizontalPosition(azimuth, altitude));

    private static readonly SkyCandidate[] Sky =
    [
        C("Saturn", 118, 31),
        C("Vega", 290, 45),
        C("Moon", 60, -14),     // below the horizon
        C("Deneb", 310, 60),
    ];

    [Fact]
    public void PointingAtSaturn_IdentifiesSaturn()
    {
        var result = SkyIdentifier.Identify(new PointingDirection(120, 30), Sky);

        Assert.NotNull(result);
        Assert.Equal("Saturn", result.Value.Candidate.Name);
        Assert.InRange(result.Value.AngularDistanceDegrees, 1.9, 2.0); // ~1.71° in azimuth (cos 31°) and 1° in altitude
    }

    [Fact]
    public void NothingWithinRadius_ReturnsNull()
    {
        Assert.Null(SkyIdentifier.Identify(new PointingDirection(200, 20), Sky));
    }

    [Fact]
    public void BelowHorizon_IsIgnored_EvenWhenPointedAt()
    {
        Assert.Null(SkyIdentifier.Identify(new PointingDirection(60, -14), Sky));
    }

    [Fact]
    public void BelowHorizon_CanBeIncluded()
    {
        var result = SkyIdentifier.Identify(new PointingDirection(60, -14), Sky, minAltitudeDegrees: -90);

        Assert.Equal("Moon", result?.Candidate.Name);
    }

    [Fact]
    public void TwoCandidatesInRange_NearestWins()
    {
        var sky = new[] { C("A", 100, 30), C("B", 103, 30) };

        var result = SkyIdentifier.Identify(new PointingDirection(102, 30), sky);

        Assert.Equal("B", result?.Candidate.Name);
    }

    [Fact]
    public void AcrossNorth_UsesTheShortWay()
    {
        var sky = new[] { C("Polaris", 0.5, 40) };

        var result = SkyIdentifier.Identify(new PointingDirection(358, 40), sky);

        Assert.Equal("Polaris", result?.Candidate.Name);
        Assert.InRange(result!.Value.AngularDistanceDegrees, 1.8, 2.0);
    }

    [Fact]
    public void Nearest_ReportsTheClosestEvenIfFar()
    {
        var result = SkyIdentifier.Nearest(new PointingDirection(200, 20), Sky);

        Assert.Equal("Saturn", result?.Candidate.Name);
        Assert.True(result!.Value.AngularDistanceDegrees > SkyIdentifier.DefaultRadiusDegrees);
    }

    [Fact]
    public void Nearest_EmptyOrAllBelowHorizon_ReturnsNull()
    {
        Assert.Null(SkyIdentifier.Nearest(new PointingDirection(0, 0), []));
        Assert.Null(SkyIdentifier.Nearest(new PointingDirection(0, 0), [C("Moon", 60, -14)]));
    }

    [Fact]
    public void BrighterStarWins_WhenADimmerOneIsOnlySlightlyCloser()
    {
        // Pointing ~0.9° from a magnitude-3 star and ~3° from a magnitude-0 one: scores ~3.9 vs ~3.
        var sky = new[]
        {
            new SkyCandidate("Dim", new HorizontalPosition(101, 30), Magnitude: 3.0),
            new SkyCandidate("Bright", new HorizontalPosition(100 - 3 / Math.Cos(30 * Math.PI / 180), 30), Magnitude: 0.0),
        };

        var result = SkyIdentifier.Identify(new PointingDirection(100, 30), sky);

        Assert.Equal("Bright", result?.Candidate.Name);
    }

    [Fact]
    public void DimStarWins_WhenClearlyUnderTheReticle()
    {
        // 0.2° from a magnitude-3 star, 4.5° from a magnitude-0 one: scores 3.2 vs 4.5.
        var sky = new[]
        {
            new SkyCandidate("Dim", new HorizontalPosition(100, 30.2), Magnitude: 3.0),
            new SkyCandidate("Bright", new HorizontalPosition(100, 34.5), Magnitude: 0.0),
        };

        var result = SkyIdentifier.Identify(new PointingDirection(100, 30), sky);

        Assert.Equal("Dim", result?.Candidate.Name);
        Assert.InRange(result!.Value.AngularDistanceDegrees, 0.19, 0.21); // reported distance is the true one
    }

    [Fact]
    public void PlanetsAreNotPenalised()
    {
        var sky = new[]
        {
            new SkyCandidate("Saturn", new HorizontalPosition(100, 32)),
            new SkyCandidate("Star", new HorizontalPosition(100, 31), Magnitude: 2.5),
        };

        var result = SkyIdentifier.Identify(new PointingDirection(100, 30), sky);

        Assert.Equal("Saturn", result?.Candidate.Name);
    }

    [Fact]
    public void BrightnessNeverPullsInSomethingOutsideTheRadius()
    {
        var sky = new[] { new SkyCandidate("Sirius", new HorizontalPosition(100, 36), Magnitude: -1.46) };

        Assert.Null(SkyIdentifier.Identify(new PointingDirection(100, 30), sky));
    }

    [Fact]
    public void Radius_IsHonouredAndValidated()
    {
        Assert.NotNull(SkyIdentifier.Identify(new PointingDirection(126, 31), Sky, radiusDegrees: 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkyIdentifier.Identify(new PointingDirection(0, 0), Sky, radiusDegrees: 0));
    }
}
