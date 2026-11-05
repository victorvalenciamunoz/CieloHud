using CieloHud.Core.Guidance;

namespace CieloHud.Core.Tests.Guidance;

public class GuidanceCalculatorTests
{
    private readonly GuidanceCalculator _calculator = new();

    [Theory]
    [InlineData(350, 10, 20)]    // target just east of north while pointing just west of it: turn right 20°
    [InlineData(10, 350, -20)]   // mirror: turn left 20°
    [InlineData(0, 180, 180)]    // directly behind: reported as +180
    [InlineData(90, 270, 180)]
    [InlineData(270, 90, 180)]
    [InlineData(45, 45, 0)]
    [InlineData(0, 359, -1)]
    public void AzimuthDelta_IsShortestSignedTurn(double pointingAzimuth, double targetAzimuth, double expectedDelta)
    {
        var guidance = _calculator.Compute(new PointingDirection(pointingAzimuth, 0), new HorizontalPosition(targetAzimuth, 0), wasOnTarget: false);

        Assert.Equal(expectedDelta, guidance.AzimuthDeltaDegrees, precision: 9);
    }

    [Theory]
    [InlineData(10, 30, 20)]
    [InlineData(30, 10, -20)]
    [InlineData(-5, 45, 50)]
    public void AltitudeDelta_IsTargetMinusPointing(double pointingAltitude, double targetAltitude, double expectedDelta)
    {
        var guidance = _calculator.Compute(new PointingDirection(0, pointingAltitude), new HorizontalPosition(0, targetAltitude), wasOnTarget: false);

        Assert.Equal(expectedDelta, guidance.AltitudeDeltaDegrees, precision: 9);
    }

    [Theory]
    [InlineData(0, 0, 90, 0, 90)]      // on the horizon, a quarter turn is 90°
    [InlineData(0, 0, 0, 45, 45)]      // same azimuth: distance is the altitude difference
    [InlineData(0, 80, 180, 80, 20)]   // near the zenith, opposite azimuths are only 20° apart
    [InlineData(0, 90, 123, 90, 0)]    // at the zenith azimuth is meaningless
    [InlineData(30, 20, 30, 20, 0)]
    [InlineData(0, 0, 180, 0, 180)]
    public void AngularDistance_IsGreatCircle(double az1, double alt1, double az2, double alt2, double expected)
    {
        Assert.Equal(expected, GuidanceCalculator.AngularDistance(az1, alt1, az2, alt2), precision: 6);
    }

    [Fact]
    public void OnTarget_EntersInsideEnterRadius()
    {
        var guidance = _calculator.Compute(new PointingDirection(0, 0), new HorizontalPosition(3.9, 0), wasOnTarget: false);

        Assert.True(guidance.IsOnTarget);
    }

    [Fact]
    public void OnTarget_DoesNotEnterBetweenRadii()
    {
        var guidance = _calculator.Compute(new PointingDirection(0, 0), new HorizontalPosition(5, 0), wasOnTarget: false);

        Assert.False(guidance.IsOnTarget);
    }

    [Fact]
    public void OnTarget_StaysBetweenRadii_WhenAlreadyOn()
    {
        var guidance = _calculator.Compute(new PointingDirection(0, 0), new HorizontalPosition(5, 0), wasOnTarget: true);

        Assert.True(guidance.IsOnTarget);
    }

    [Fact]
    public void OnTarget_LeavesBeyondExitRadius()
    {
        var guidance = _calculator.Compute(new PointingDirection(0, 0), new HorizontalPosition(6.1, 0), wasOnTarget: true);

        Assert.False(guidance.IsOnTarget);
    }

    [Fact]
    public void Settings_AreHonoured()
    {
        var wide = new GuidanceCalculator(new GuidanceSettings { EnterRadiusDegrees = 10, ExitRadiusDegrees = 12 });

        Assert.True(wide.Compute(new PointingDirection(0, 0), new HorizontalPosition(9, 0), wasOnTarget: false).IsOnTarget);
    }

    [Fact]
    public void Settings_ExitSmallerThanEnter_Throws()
    {
        Assert.Throws<ArgumentException>(() => new GuidanceCalculator(new GuidanceSettings { EnterRadiusDegrees = 6, ExitRadiusDegrees = 4 }));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(180, 180)]
    [InlineData(181, -179)]
    [InlineData(-180, 180)]
    [InlineData(540, 180)]
    [InlineData(-90, -90)]
    public void WrapToHalfTurn_MapsIntoMinus180To180(double input, double expected)
    {
        Assert.Equal(expected, GuidanceCalculator.WrapToHalfTurn(input), precision: 9);
    }

    [Fact]
    public void PointingDirection_NormalizesAzimuthAndRejectsBadAltitude()
    {
        Assert.Equal(270, new PointingDirection(-90, 0).AzimuthDegrees);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PointingDirection(0, 91));
    }
}
