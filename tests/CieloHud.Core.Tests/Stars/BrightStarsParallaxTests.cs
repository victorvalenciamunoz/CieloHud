namespace CieloHud.Core.Tests.Stars;

/// <summary>
/// Parallaxes from the Hipparcos new reduction (van Leeuwen 2007), checked against VizieR I/311/hip2 on 2026-10-07
/// (<c>SELECT HIP, RArad, DErad, Plx, e_Plx FROM "I/311/hip2" WHERE Hpmag &lt; 4</c>). Among them, stars where SIMBAD gives
/// Gaia instead and Gaia disagrees by more than 3 standard errors (Tarazed, Xamidimura), and the two SIMBAD has none for
/// (Acrab, Algieba): decision 044.
/// </summary>
public class BrightStarsParallaxTests
{
    [Theory]
    [InlineData("Sirius", 32349, 379.21, 1.58)]
    [InlineData("Betelgeuse", 27989, 6.55, 0.83)]
    [InlineData("Polaris", 11767, 7.54, 0.11)]
    [InlineData("Deneb", 102098, 2.31, 0.32)]
    [InlineData("Alnilam", 26311, 1.65, 0.45)]
    [InlineData("Tarazed", 97278, 8.26, 0.17)]      // SIMBAD: Gaia EDR3 5.59 ± 0.39
    [InlineData("Xamidimura", 82514, 6.51, 0.91)]   // SIMBAD: Gaia EDR3 1.87 ± 0.74
    [InlineData("Acrab", 78820, 8.07, 0.78)]        // SIMBAD: none
    [InlineData("Algieba", 50583, 25.07, 0.52)]     // SIMBAD: none
    public void Parallax_FromHipparcos(string name, int hipparcos, double parallax, double error)
    {
        var star = BrightStars.Get(name);

        Assert.Equal(hipparcos, star.Hipparcos);
        Assert.Equal(parallax, star.ParallaxMas);
        Assert.Equal(error, star.ParallaxErrorMas);
    }

    [Fact]
    public void EveryStar_HasItsOwnHipparcosEntryAndAPositiveParallax()
    {
        Assert.Equal(BrightStars.All.Count, BrightStars.All.Select(s => s.Hipparcos).Distinct().Count());
        Assert.All(BrightStars.All, s =>
        {
            Assert.True(s.ParallaxMas > 0, s.Name);
            Assert.True(s.ParallaxErrorMas > 0, s.Name);
        });
    }

    [Fact]
    public void Certainty_HowManyOfEach()
    {
        // 117 within 5 %, 34 within 20 %, 3 within 50 % (Alnilam, Aludra, omi02 CMa) and Almaaz beyond.
        var counts = BrightStars.All.GroupBy(s => StarLight.Of(s).Certainty).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(117, counts[DistanceCertainty.Precise]);
        Assert.Equal(34, counts[DistanceCertainty.About]);
        Assert.Equal(3, counts[DistanceCertainty.Between]);
        Assert.Equal(1, counts[DistanceCertainty.MoreThan]);
    }
}
