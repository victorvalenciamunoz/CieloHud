using CieloHud.Core.Constellations;

namespace CieloHud.Core.Tests.Constellations;

/// <summary>
/// The figures come from a different source (d3-celestial) than the star catalog (SIMBAD), so checking that the
/// well-known figures pass through our catalog stars is an independent check of both.
/// </summary>
public class ConstellationFiguresTests
{
    private static readonly Observer Madrid = new(40.4168, -3.7038, 650);
    private static readonly DateTimeOffset Instant = DateTimeOffset.Parse("2026-10-03T04:00:00Z");

    [Fact]
    public void All88Constellations_HaveAFigure()
    {
        Assert.Equal(88, ConstellationFigures.All.Count);
        Assert.Equal(88, ConstellationFigures.All.Select(f => f.Symbol).Distinct().Count());
        foreach (var figure in ConstellationFigures.All)
        {
            Assert.InRange(figure.Rank, 1, 3);
            Assert.NotEmpty(figure.Lines);
            Assert.All(figure.Lines, line => Assert.True(line.Count >= 2, $"{figure.Symbol} has a line with fewer than 2 vertices"));
            Assert.All(figure.Lines.SelectMany(l => l), p =>
            {
                Assert.InRange(p.RightAscensionDegrees, 0, 360);
                Assert.InRange(p.DeclinationDegrees, -90, 90);
            });
        }
    }

    [Fact]
    public void Serpens_KeepsBothPieces()
    {
        Assert.Equal(2, ConstellationFigures.Get("Ser")!.Lines.Count);
    }

    [Fact]
    public void UnknownSymbol_ReturnsNull()
    {
        Assert.Null(ConstellationFigures.Get("Xyz"));
    }

    [Theory]
    [InlineData("Ori", new[] { "Betelgeuse", "Rigel", "Bellatrix", "Saiph", "Alnitak", "Alnilam", "Mintaka" })]
    [InlineData("UMa", new[] { "Dubhe", "Merak", "Phecda", "Alioth", "Mizar", "Alkaid" })]
    [InlineData("Cas", new[] { "Schedar", "Caph", "gam Cas" })]
    [InlineData("Cyg", new[] { "Deneb", "Sadr", "Aljanah" })]
    [InlineData("Lyr", new[] { "Vega" })]
    [InlineData("UMi", new[] { "Polaris", "Kochab" })]
    [InlineData("Sco", new[] { "Antares", "Shaula", "Acrab", "Dschubba" })]
    public void Figure_PassesThroughItsBrightStars(string symbol, string[] stars)
    {
        var vertices = ConstellationFigures.Get(symbol)!.Lines.SelectMany(l => l).ToList();

        foreach (var name in stars)
        {
            var star = BrightStars.Get(name);
            var nearest = vertices.Min(v => GuidanceCalculator.AngularDistance(
                v.RightAscensionDegrees, v.DeclinationDegrees, star.RightAscensionDegrees, star.DeclinationDegrees));
            Assert.True(nearest < 0.02, $"{symbol} figure misses {name} by {nearest:F3}°");
        }
    }

    [Fact]
    public void Locator_PutsTheVertexOnTheStar()
    {
        var lines = new AstronomyEngineConstellationFigureLocator().Locate("Ori", Madrid, Instant);
        var betelgeuse = new AstronomyEngineStarService().Locate(BrightStars.Get("Betelgeuse"), Madrid, Instant);

        var nearest = lines.SelectMany(l => l).Min(v => GuidanceCalculator.AngularDistance(
            v.AzimuthDegrees, v.AltitudeDegrees, betelgeuse.AzimuthDegrees, betelgeuse.AltitudeDegrees));

        Assert.True(nearest < 0.02, $"nearest Orion vertex is {nearest:F3}° from Betelgeuse");
    }

    [Fact]
    public void Locator_KeepsTheShapeOfTheFigure()
    {
        var figure = ConstellationFigures.Get("UMa")!;

        var lines = new AstronomyEngineConstellationFigureLocator().Locate("UMa", Madrid, Instant);

        Assert.Equal(figure.Lines.Count, lines.Count);
        Assert.Equal(figure.Lines.Select(l => l.Count), lines.Select(l => l.Count));
    }

    [Fact]
    public void Locator_UnknownSymbol_ReturnsEmpty()
    {
        Assert.Empty(new AstronomyEngineConstellationFigureLocator().Locate("Xyz", Madrid, Instant));
    }
}
