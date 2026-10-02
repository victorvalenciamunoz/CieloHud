namespace CieloHud.Core.Tests.Sky;

public class AzimuthTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(359.9, 359.9)]
    [InlineData(360, 0)]
    [InlineData(725, 5)]
    [InlineData(-10, 350)]
    [InlineData(-360, 0)]
    public void Normalize_WrapsInto0To360(double input, double expected)
    {
        Assert.Equal(expected, Azimuth.Normalize(input), precision: 9);
    }

    [Fact]
    public void Normalize_TinyNegative_StaysBelow360()
    {
        var result = Azimuth.Normalize(-1e-15);

        Assert.InRange(result, 0, 360 - double.Epsilon);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Normalize_NonFinite_Throws(double input)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Azimuth.Normalize(input));
    }

    [Theory]
    [InlineData(0, CardinalPoint.N)]
    [InlineData(22.4, CardinalPoint.N)]
    [InlineData(22.5, CardinalPoint.NE)]
    [InlineData(45, CardinalPoint.NE)]
    [InlineData(67.4, CardinalPoint.NE)]
    [InlineData(67.5, CardinalPoint.E)]
    [InlineData(90, CardinalPoint.E)]
    [InlineData(135, CardinalPoint.SE)]
    [InlineData(180, CardinalPoint.S)]
    [InlineData(225, CardinalPoint.SW)]
    [InlineData(270, CardinalPoint.W)]
    [InlineData(315, CardinalPoint.NW)]
    [InlineData(337.4, CardinalPoint.NW)]
    [InlineData(337.5, CardinalPoint.N)]
    [InlineData(359.9, CardinalPoint.N)]
    [InlineData(360, CardinalPoint.N)]
    [InlineData(-45, CardinalPoint.NW)]
    [InlineData(450, CardinalPoint.E)]
    public void ToCardinalPoint_MapsSectorsOf45Degrees(double azimuth, CardinalPoint expected)
    {
        Assert.Equal(expected, Azimuth.ToCardinalPoint(azimuth));
    }
}
