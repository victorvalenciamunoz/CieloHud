namespace CieloHud.Core.Tests.Sky;

public class HorizontalPositionTests
{
    [Fact]
    public void Constructor_NormalizesAzimuth()
    {
        var position = new HorizontalPosition(azimuthDegrees: -90, altitudeDegrees: 10);

        Assert.Equal(270, position.AzimuthDegrees);
        Assert.Equal(CardinalPoint.W, position.CardinalPoint);
    }

    [Theory]
    [InlineData(0.1, true)]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    public void IsAboveHorizon_DependsOnAltitudeSign(double altitude, bool expected)
    {
        var position = new HorizontalPosition(0, altitude);

        Assert.Equal(expected, position.IsAboveHorizon);
    }

    [Theory]
    [InlineData(90.1)]
    [InlineData(-90.1)]
    [InlineData(double.NaN)]
    public void Constructor_RejectsAltitudeOutOfRange(double altitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HorizontalPosition(0, altitude));
    }

    [Fact]
    public void Constructor_RejectsNegativeDistance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HorizontalPosition(0, 0, distanceKm: -1));
    }

    [Fact]
    public void Distance_IsOptional()
    {
        var position = new HorizontalPosition(0, 0);

        Assert.Null(position.DistanceKm);
    }
}
