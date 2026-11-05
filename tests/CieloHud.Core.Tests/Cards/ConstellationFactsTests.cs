using CieloHud.Core.Cards;
using CieloHud.Core.Constellations;

namespace CieloHud.Core.Tests.Cards;

public class ConstellationFactsTests
{
    private const double Madrid = 40.42;
    private const double Sydney = -33.87;

    [Theory]
    [InlineData("UMi", Madrid, ConstellationSight.NeverSets)]
    [InlineData("Cep", Madrid, ConstellationSight.NeverSets)] // down to 53.5°, above 90° − 40.42°
    [InlineData("UMa", Madrid, ConstellationSight.Whole)] // part of it never sets, not all
    [InlineData("Ori", Madrid, ConstellationSight.Whole)]
    [InlineData("Sco", Madrid, ConstellationSight.Whole)] // its tail, at −45.7°, rises 4° at most
    [InlineData("Cen", Madrid, ConstellationSight.Partly)]
    [InlineData("Cru", Madrid, ConstellationSight.NeverRises)]
    [InlineData("Oct", Sydney, ConstellationSight.NeverSets)]
    [InlineData("Cru", Sydney, ConstellationSight.Whole)] // −55.7° is just short of never setting from Sydney (−56.1°)
    [InlineData("UMi", Sydney, ConstellationSight.NeverRises)]
    [InlineData("UMa", Sydney, ConstellationSight.Partly)]
    public void Sight_FromALatitude(string symbol, double latitude, ConstellationSight sight)
    {
        Assert.Equal(sight, ConstellationFacts.SightFrom(ConstellationExtents.Get(symbol)!, latitude));
    }

    [Fact]
    public void FromTheEquator_AllRiseWhole_ButThoseAroundThePoles()
    {
        var partly = ConstellationExtents.All.Where(e => ConstellationFacts.SightFrom(e, 0) != ConstellationSight.Whole).Select(e => e.Symbol);
        Assert.Equal(["Oct", "UMi"], partly.Order(StringComparer.Ordinal));
        Assert.Equal(ConstellationSight.Partly, ConstellationFacts.SightFrom(ConstellationExtents.Get("UMi")!, 0));
    }

    [Theory]
    // Ridpath's table, latitudes from which each rises whole: Orion up to 79° N, Andromeda down to 37° S, the Southern Cross up to 25° N.
    [InlineData("Ori", 78.5, 79.5)]
    [InlineData("And", -36.5, -37.5)]
    [InlineData("Cru", 25, 25.5)]
    public void WholeUpToRidpathsLatitude(string symbol, double whole, double partly)
    {
        var extent = ConstellationExtents.Get(symbol)!;
        Assert.Equal(ConstellationSight.Whole, ConstellationFacts.SightFrom(extent, whole));
        Assert.Equal(ConstellationSight.Partly, ConstellationFacts.SightFrom(extent, partly));
    }

    [Fact]
    public void Orion_FromMadrid()
    {
        var facts = ConstellationFacts.Of("Ori", new Observer(Madrid, -3.70));
        Assert.Equal(0.01440, facts.SkyFraction, 0.00002);
        Assert.Equal(26, facts.SizeRank);
        Assert.Equal(ConstellationSight.Whole, facts.Sight);
        Assert.Equal("Rigel", facts.BrightestStars![0].Name);
    }

    [Fact]
    public void Stars_NoneOrUnknown()
    {
        var observer = new Observer(Madrid, -3.70);
        Assert.Empty(ConstellationFacts.Of("Cnc", observer).BrightestStars!);
        Assert.Null(ConstellationFacts.Of("Cen", observer).BrightestStars);
    }

    [Fact]
    public void UnknownSymbol_Throws()
    {
        Assert.Throws<ArgumentException>(() => ConstellationFacts.Of("Xyz", new Observer(Madrid, -3.70)));
    }
}
