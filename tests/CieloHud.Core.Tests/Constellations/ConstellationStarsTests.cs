using CieloHud.Core.Constellations;
using CieloHud.Core.Stars;

namespace CieloHud.Core.Tests.Constellations;

public class ConstellationStarsTests
{
    /// <summary>
    /// The boundaries (Roman 1987) and the Bayer designations (SIMBAD) come from different sources: every star of the catalog
    /// falls inside the constellation its designation names.
    /// </summary>
    [Fact]
    public void EveryCatalogStar_IsInTheConstellationOfItsDesignation()
    {
        Assert.All(BrightStars.All, s =>
            Assert.Equal(s.Designation.Split(' ')[^1], ConstellationStars.SymbolAt(s.RightAscensionDegrees, s.DeclinationDegrees)));
    }

    [Fact]
    public void EveryCatalogStar_IsInExactlyOneConstellation()
    {
        Assert.Equal(BrightStars.All.Count, ConstellationExtents.All.Sum(e => ConstellationStars.In(e.Symbol).Count));
    }

    [Fact]
    public void Orion_BrightestFirst()
    {
        var stars = ConstellationStars.In("Ori");
        Assert.Equal(["Rigel", "Betelgeuse", "Bellatrix", "Alnilam", "Alnitak", "Saiph", "Mintaka", "Hatysa"], stars.Select(s => s.Name));
    }

    [Theory]
    [InlineData("Cnc")]
    [InlineData("Psc")]
    [InlineData("Lyn")]
    public void Faint_HaveNone(string symbol)
    {
        Assert.Empty(ConstellationStars.In(symbol));
    }

    [Theory]
    [InlineData("Ori", true)]
    [InlineData("Sco", true)] // reaches −45.7°
    [InlineData("Eri", false)] // Achernar, at −57°, is not in the catalog
    [InlineData("Cen", false)] // nor Alpha Centauri
    [InlineData("Cru", false)]
    public void CatalogCovers_OnlyNorthOfItsLimit(string symbol, bool covers)
    {
        Assert.Equal(covers, ConstellationStars.CatalogCovers(symbol));
    }

    [Fact]
    public void CatalogCovers_60Of88()
    {
        Assert.Equal(60, ConstellationExtents.All.Count(e => ConstellationStars.CatalogCovers(e.Symbol)));
    }

    [Fact]
    public void TheCatalog_StopsAtItsSouthernLimit()
    {
        Assert.All(BrightStars.All, s => Assert.True(s.DeclinationDegrees > BrightStars.SouthernLimitDegrees, s.Name));
    }
}
