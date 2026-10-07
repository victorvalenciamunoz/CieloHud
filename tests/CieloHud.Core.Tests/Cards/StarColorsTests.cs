namespace CieloHud.Core.Tests.Cards;

public class StarColorsTests
{
    [Theory]
    [InlineData("Rigel", StarColor.Bluish)]          // B8Ia
    [InlineData("Spica", StarColor.Bluish)]          // B1V
    [InlineData("Vega", StarColor.White)]            // A0V
    [InlineData("Sirius", StarColor.White)]          // A0mA1Va
    [InlineData("Procyon", StarColor.YellowishWhite)] // F5IV-V+DQZ
    [InlineData("Polaris", StarColor.YellowishWhite)] // F8Ib
    [InlineData("Capella", StarColor.Yellowish)]     // G3III:
    [InlineData("Arcturus", StarColor.Orange)]       // K1.5IIIFe-0.5
    [InlineData("Aldebaran", StarColor.Orange)]      // K5+III: B−V would call it reddish
    [InlineData("Betelgeuse", StarColor.Reddish)]    // M1-M2Ia-Iab
    [InlineData("Antares", StarColor.Reddish)]       // M1.5Iab+B2Vn
    public void FamousStars(string name, StarColor expected)
    {
        Assert.Equal(expected, StarColors.Of(BrightStars.Get(name)));
    }

    [Theory]
    [InlineData("kA4hA5mA5Va", StarColor.White)]     // Sheratan: Am star, the "k" prefix is not a class
    [InlineData("WC8+O7.5III-V", StarColor.Bluish)]  // gamma Velorum: Wolf-Rayet
    [InlineData("O9.2IVnn", StarColor.Bluish)]
    [InlineData("G9.5IIICH-1", StarColor.Yellowish)]
    public void PeculiarTypes(string spectralType, StarColor expected)
    {
        Assert.Equal(expected, StarColors.Of(spectralType));
    }

    [Theory]
    [InlineData("")]
    [InlineData("pec")]
    public void NoClassLetter_Throws(string spectralType)
    {
        Assert.Throws<FormatException>(() => StarColors.Of(spectralType));
    }

    [Fact]
    public void EveryStarInTheCatalog_HasAColor()
    {
        Assert.All(BrightStars.All, s => StarColors.Of(s));
    }

    [Theory]
    [InlineData(StarColor.Bluish, "Su luz es azulada")]
    [InlineData(StarColor.White, "Su luz es blanca")]
    [InlineData(StarColor.YellowishWhite, "Su luz es de un blanco amarillento")]
    [InlineData(StarColor.Yellowish, "Su luz es amarillenta")]
    [InlineData(StarColor.Orange, "Su luz es anaranjada")]
    [InlineData(StarColor.Reddish, "Su luz es rojiza")]
    public void ColorText(StarColor color, string expected)
    {
        Assert.Equal(expected, FactsText.StarColorName(color));
    }

    [Theory]
    [InlineData(-1.46, "−1,5")]
    [InlineData(0.03, "0,0")]
    [InlineData(0.42, "0,4")]
    [InlineData(2.02, "2,0")]
    [InlineData(-0.05, "−0,1")]
    public void MagnitudeText_ExplainsTheBackwardsScale(double magnitude, string number)
    {
        Assert.Equal($"Brillo: magnitud {number} (cuanto menor, más brilla; desde ciudad se ven hasta la 3)", FactsText.StarMagnitude(magnitude));
    }
}
