namespace CieloHud.Core.Tests.Cards;

public class SaturnShapeTests
{
    [Theory]
    [InlineData(-7.36, 317.05)]
    [InlineData(26.9, 23.4)]
    [InlineData(0, 90)]
    public void Rings_EllipseIsTheSineOfTheTilt(double tilt, double pole)
    {
        var shape = SaturnShape.Of(tilt, pole);

        Assert.Equal(Math.Abs(Math.Sin(tilt * Math.PI / 180)), shape.RingMinorToMajor, 9);
        Assert.Equal(pole, shape.PoleDegrees);
    }

    [Fact]
    public void NorthFaceShows_RingsPassInFrontOnTheSouthSide()
    {
        Assert.Equal(200, SaturnShape.Of(tiltDegrees: 7, northPoleDegrees: 20).FrontDegrees, 9);
        Assert.Equal(20, SaturnShape.Of(tiltDegrees: 7, northPoleDegrees: 200).FrontDegrees, 9);
    }

    [Fact]
    public void SouthFaceShows_RingsPassInFrontOnTheNorthSide()
    {
        // Now (October 2026): the south face, at 7°.
        Assert.Equal(317.05, SaturnShape.Of(tiltDegrees: -7.36, northPoleDegrees: 317.05).FrontDegrees, 9);
    }

    [Fact]
    public void Globe_EdgeOnShowsAllItsFlattening()
    {
        Assert.Equal(54_364.0 / 60_268, SaturnShape.Of(0, 0).GlobePolarRadius, 9);
    }

    [Fact]
    public void Globe_LessFlattenedAsTheRingsOpen()
    {
        var edgeOn = SaturnShape.Of(0, 0).GlobePolarRadius;
        var now = SaturnShape.Of(-7.36, 0).GlobePolarRadius;
        var widest = SaturnShape.Of(26.9, 0).GlobePolarRadius;

        Assert.True(edgeOn < now && now < widest && widest < 1);
    }

    [Fact]
    public void Rings_FromRingBToRingA()
    {
        Assert.Equal(1.526, SaturnShape.RingInnerRadius, 3);
        Assert.Equal(2.270, SaturnShape.RingOuterRadius, 3);
    }
}
