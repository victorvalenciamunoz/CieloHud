namespace CieloHud.Core.Tests.Cards;

public class StarLightTests
{
    private const char Space = ' ';

    [Fact]
    public void Years_AreParsecsTimesLightYearsPerParsec()
    {
        // 379.21 mas: 2.637 pc, 8.601 light years.
        var light = StarLight.Of(379.21, 1.58);

        Assert.Equal(1000 / 379.21 * 3.261_563_777, light.Years, 9);
        Assert.Equal(8.601, light.Years, 3);
    }

    [Theory]
    [InlineData(379.21, 1.58, DistanceCertainty.Precise)]   // Sirius, 0.4 %
    [InlineData(10, 0.5, DistanceCertainty.Precise)]        // 5 % exactly
    [InlineData(6.55, 0.83, DistanceCertainty.About)]       // Betelgeuse, 13 %
    [InlineData(10, 2, DistanceCertainty.About)]            // 20 % exactly
    [InlineData(1.65, 0.45, DistanceCertainty.Between)]     // Alnilam, 27 %
    [InlineData(10, 5, DistanceCertainty.Between)]          // 50 % exactly
    [InlineData(1.53, 1.29, DistanceCertainty.MoreThan)]    // Almaaz, 84 %
    [InlineData(1, 1.2, DistanceCertainty.MoreThan)]        // parallax smaller than its error
    public void Certainty_ByRelativeError(double parallax, double error, DistanceCertainty expected)
    {
        Assert.Equal(expected, StarLight.Of(parallax, error).Certainty);
    }

    [Fact]
    public void Range_OneStandardErrorEitherSide()
    {
        var light = StarLight.Of(1.65, 0.45);

        Assert.Equal(1000 / 2.10 * StarLight.LightYearsPerParsec, light.NearYears, 6);
        Assert.Equal(1000 / 1.20 * StarLight.LightYearsPerParsec, light.FarYears, 6);
    }

    [Fact]
    public void ParallaxNoLargerThanItsError_NoFarEnd()
    {
        Assert.Equal(double.PositiveInfinity, StarLight.Of(1, 1).FarYears);
    }

    [Theory]
    [InlineData(0, 0.1)]
    [InlineData(-1, 0.1)]
    [InlineData(1, -0.1)]
    public void Invalid_Throws(double parallax, double error)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StarLight.Of(parallax, error));
    }

    [Theory]
    [InlineData("Sirio", 379.21, 1.58, "Esta luz salió de Sirio hace 8,6 años")]
    [InlineData("Vega", 130.23, 0.36, "Esta luz salió de Vega hace 25 años")]
    [InlineData("Arturo", 88.83, 0.54, "Esta luz salió de Arturo hace 37 años")]
    [InlineData("Polar", 7.54, 0.11, "Esta luz salió de Polar hace 430 años")]
    [InlineData("Betelgeuse", 6.55, 0.83, "Esta luz salió de Betelgeuse hace unos 500 años")]
    [InlineData("Deneb", 2.31, 0.32, "Esta luz salió de Deneb hace unos 1400 años")]
    [InlineData("Alnilam", 1.65, 0.45, "Esta luz salió de Alnilam hace entre 1600 y 2700 años")]
    [InlineData("Almaaz", 1.53, 1.29, "Esta luz salió de Almaaz hace más de 1100 años")]
    public void Text_TwoFiguresWordedByCertainty(string name, double parallax, double error, string expected)
    {
        Assert.Equal(expected, FactsText.StarLight(name, StarLight.Of(parallax, error)));
    }

    [Fact]
    public void Text_FarAwayAndPrecise_GroupsThousands()
    {
        // 0.25 mas ± 0.01: 13 046 light years.
        Assert.Equal($"Esta luz salió de X hace 13{Space}000 años", FactsText.StarLight("X", StarLight.Of(0.25, 0.01)));
    }

    [Fact]
    public void Text_MoreThan_RoundsDownSoItStaysTrue()
    {
        // Near end 1157 light years: "más de 1100", never "más de 1200".
        var light = StarLight.Of(1.53, 1.29);

        Assert.True(light.NearYears > 1100 && light.NearYears < 1200);
        Assert.EndsWith("más de 1100 años", FactsText.StarLight("Almaaz", light));
    }
}
