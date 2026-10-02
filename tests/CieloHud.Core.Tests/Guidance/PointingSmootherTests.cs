namespace CieloHud.Core.Tests.Guidance;

public class PointingSmootherTests
{
    [Fact]
    public void FirstReading_IsReturnedUnchanged()
    {
        var smoother = new PointingSmoother(0.2);

        var result = smoother.Push(new PointingDirection(123, 45));

        Assert.Equal(new PointingDirection(123, 45), result);
        Assert.Equal(result, smoother.Current);
    }

    [Fact]
    public void MovesAFractionTowardsTheNewReading()
    {
        var smoother = new PointingSmoother(0.25);
        smoother.Push(new PointingDirection(100, 10));

        var result = smoother.Push(new PointingDirection(140, 30));

        Assert.Equal(110, result.AzimuthDegrees, precision: 9);
        Assert.Equal(15, result.AltitudeDegrees, precision: 9);
    }

    [Fact]
    public void AzimuthCrossesNorth_TheShortWay()
    {
        var smoother = new PointingSmoother(0.5);
        smoother.Push(new PointingDirection(350, 0));

        var result = smoother.Push(new PointingDirection(10, 0));

        Assert.Equal(0, result.AzimuthDegrees, precision: 9);
    }

    [Fact]
    public void AzimuthCrossesNorth_TheOtherWay()
    {
        var smoother = new PointingSmoother(0.5);
        smoother.Push(new PointingDirection(10, 0));

        var result = smoother.Push(new PointingDirection(350, 0));

        Assert.Equal(0, result.AzimuthDegrees, precision: 9);
    }

    [Fact]
    public void ConvergesToASteadyReading()
    {
        var smoother = new PointingSmoother(0.2);
        smoother.Push(new PointingDirection(0, 0));

        PointingDirection result = default;
        for (var i = 0; i < 60; i++)
            result = smoother.Push(new PointingDirection(90, 40));

        Assert.Equal(90, result.AzimuthDegrees, 1e-3);
        Assert.Equal(40, result.AltitudeDegrees, 1e-3);
    }

    [Fact]
    public void Reset_ForgetsHistory()
    {
        var smoother = new PointingSmoother(0.2);
        smoother.Push(new PointingDirection(0, 0));
        smoother.Reset();

        var result = smoother.Push(new PointingDirection(180, 50));

        Assert.Equal(new PointingDirection(180, 50), result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void InvalidAlpha_Throws(double alpha)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PointingSmoother(alpha));
    }
}
