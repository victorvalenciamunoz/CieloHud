namespace CieloHud.Core.Tests.Cards;

/// <summary>The names the app shows, now in Core so the star choices can search them (decision 054).</summary>
public class SpanishNamesTests
{
    [Theory]
    [InlineData("alf CMa", "Sirio")] // usual Spanish form
    [InlineData("alf UMi", "Estrella Polar")]
    [InlineData("alf Lyr", "Vega")] // IAU name, the same in Spanish
    [InlineData("gam Cas", "Gamma de Casiopea")] // no proper name: Bayer designation in words
    [InlineData("gam02 Vel", "Gamma 2 de la Vela")]
    [InlineData("eps Per", "Épsilon de Perseo")]
    [InlineData("eps Crv", "Épsilon del Cuervo")]
    public void Star_ShowsItsSpanishName(string designation, string name)
    {
        Assert.Equal(name, SpanishNames.Star(BrightStars.All.Single(s => s.Designation == designation)));
    }

    [Fact]
    public void Constellations_HaveAllEightyEightSymbols()
    {
        Assert.Equal(88, SpanishNames.Constellations.Count);
        Assert.All(CieloHud.Core.Constellations.ConstellationExtents.All, c => Assert.True(SpanishNames.Constellations.ContainsKey(c.Symbol), c.Symbol));
    }

    [Theory]
    [InlineData("la Osa Mayor", "Osa Mayor")]
    [InlineData("los Perros de Caza", "Perros de Caza")]
    [InlineData("Orión", "Orión")]
    public void WithoutArticle_DropsTheArticle(string name, string withoutArticle)
    {
        Assert.Equal(withoutArticle, SpanishNames.WithoutArticle(name));
    }
}
