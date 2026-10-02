namespace CieloHud.Core.Tests.Sky;

public class ObserverTests
{
    [Fact]
    public void Constructor_StoresValues()
    {
        var madrid = new Observer(latitudeDegrees: 40.4168, longitudeDegrees: -3.7038, altitudeMeters: 650);

        Assert.Equal(40.4168, madrid.LatitudeDegrees);
        Assert.Equal(-3.7038, madrid.LongitudeDegrees);
        Assert.Equal(650, madrid.AltitudeMeters);
    }

    [Fact]
    public void Altitude_DefaultsToSeaLevel()
    {
        var observer = new Observer(0, 0);

        Assert.Equal(0, observer.AltitudeMeters);
    }

    [Theory]
    [InlineData(90.1, 0)]
    [InlineData(-90.1, 0)]
    [InlineData(0, 180.1)]
    [InlineData(0, -180.1)]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.NaN)]
    public void Constructor_RejectsOutOfRangeCoordinates(double latitude, double longitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Observer(latitude, longitude));
    }

    [Fact]
    public void Equality_IsByValue()
    {
        var a = new Observer(40.4168, -3.7038, 650);
        var b = new Observer(40.4168, -3.7038, 650);

        Assert.Equal(a, b);
    }
}
