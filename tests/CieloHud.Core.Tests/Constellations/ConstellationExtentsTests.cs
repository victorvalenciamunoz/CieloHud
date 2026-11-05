using CieloHud.Core.Constellations;

namespace CieloHud.Core.Tests.Constellations;

/// <summary>
/// The extents are generated from Astronomy Engine's boundaries (Roman 1987). They are checked here against two other sources:
/// Levin's areas and their ranks, as Ian Ridpath's table of the constellations gives them, and the J2000 boundaries of CDS VI/49
/// (Davenhall &amp; Leggett 1989), queried on 2026-10-08 (decision 047).
/// </summary>
public class ConstellationExtentsTests
{
    [Fact]
    public void The88_CoverTheWholeSky()
    {
        Assert.Equal(88, ConstellationExtents.All.Count);
        Assert.Equal(
            ConstellationFigures.All.Select(f => f.Symbol).Order(StringComparer.Ordinal),
            ConstellationExtents.All.Select(e => e.Symbol).Order(StringComparer.Ordinal));
        Assert.Equal(ConstellationExtents.SkySquareDegrees, ConstellationExtents.All.Sum(e => e.AreaSquareDegrees), 0.5);
        Assert.Equal(41_252.96, ConstellationExtents.SkySquareDegrees, 0.01);
    }

    [Theory]
    [InlineData("Hya", 1302.8)]
    [InlineData("Vir", 1294.4)]
    [InlineData("UMa", 1279.7)]
    [InlineData("Cet", 1231.4)]
    [InlineData("Leo", 947.0)]
    [InlineData("Ser", 636.9)] // Levin gave its two halves apart; Ridpath adds them up
    [InlineData("Ori", 594.1)]
    [InlineData("Oct", 291.0)]
    [InlineData("Lyr", 286.5)]
    [InlineData("UMi", 255.9)]
    [InlineData("Equ", 71.6)]
    [InlineData("Cru", 68.4)]
    public void Area_MatchesLevin(string symbol, double squareDegrees)
    {
        Assert.Equal(squareDegrees, ConstellationExtents.Get(symbol)!.AreaSquareDegrees, 0.2);
    }

    [Theory]
    [InlineData("Hya", 1)]
    [InlineData("Vir", 2)]
    [InlineData("UMa", 3)]
    [InlineData("Ser", 23)]
    [InlineData("Ori", 26)]
    [InlineData("Tuc", 48)] // 294.6 square degrees
    [InlineData("Ind", 49)] // 294.0
    [InlineData("Oct", 50)]
    [InlineData("UMi", 56)]
    [InlineData("Dor", 72)] // 179.2
    [InlineData("CrB", 73)] // 178.7
    [InlineData("Tri", 78)] // 131.8
    [InlineData("Cha", 79)] // 131.6
    [InlineData("Equ", 87)]
    [InlineData("Cru", 88)]
    public void SizeRank_MatchesRidpath(string symbol, int rank)
    {
        Assert.Equal(rank, ConstellationExtents.SizeRank(symbol));
    }

    [Fact]
    public void SizeRanks_Are1To88()
    {
        Assert.Equal(Enumerable.Range(1, 88), ConstellationExtents.All.Select(e => ConstellationExtents.SizeRank(e.Symbol)).Order());
    }

    [Theory]
    [InlineData("And", 21.677, 53.187)]
    [InlineData("Ori", -10.979, 22.876)]
    [InlineData("Leo", -6.692, 32.969)] // Ridpath's table says 82° N for it, 1.3° off; the CDS and these extents agree
    [InlineData("Hya", -35.696, 6.630)]
    [InlineData("UMa", 28.304, 73.138)]
    [InlineData("Cen", -64.696, -29.995)]
    [InlineData("Cru", -64.696, -55.677)]
    [InlineData("Dor", -70.104, -48.670)]
    [InlineData("Ser", -16.140, 25.664)] // Cauda reaches further south, Caput further north
    public void DeclinationRange_MatchesCds(string symbol, double south, double north)
    {
        var extent = ConstellationExtents.Get(symbol)!;
        Assert.Equal(south, extent.MinDeclinationDegrees, 0.03);
        Assert.Equal(north, extent.MaxDeclinationDegrees, 0.03);
    }

    [Fact]
    public void ThePoles_AreInsideTheLittleBearAndTheOctant()
    {
        // The pole is inside them, not on their boundary: the CDS boundaries stop at 88.7° and −74.3°… −85.3°.
        Assert.Equal(90, ConstellationExtents.Get("UMi")!.MaxDeclinationDegrees);
        Assert.Equal(65.400, ConstellationExtents.Get("UMi")!.MinDeclinationDegrees, 0.03);
        Assert.Equal(-90, ConstellationExtents.Get("Oct")!.MinDeclinationDegrees);
        Assert.Equal(-74.304, ConstellationExtents.Get("Oct")!.MaxDeclinationDegrees, 0.03);
    }

    [Fact]
    public void UnknownSymbol()
    {
        Assert.Null(ConstellationExtents.Get("Xyz"));
        Assert.Throws<ArgumentException>(() => ConstellationExtents.SizeRank("Xyz"));
    }
}
