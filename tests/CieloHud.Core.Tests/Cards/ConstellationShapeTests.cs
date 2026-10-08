using CieloHud.Core.Cards;
using CieloHud.Core.Constellations;
using CieloHud.Core.Guidance;
using CieloHud.Core.Stars;

namespace CieloHud.Core.Tests.Cards;

public class ConstellationShapeTests
{
    private static readonly Observer Humanes = new(40.25, -3.83, 680);

    /// <summary>Orion high in the south, before dawn.</summary>
    private static readonly DateTimeOffset OrionHigh = DateTimeOffset.Parse("2026-10-09T05:00:00Z");

    private readonly AstronomyEngineStarService _stars = new();

    [Fact]
    public void Stars_AtTheStereographicDistanceFromTheCenter()
    {
        var shape = ConstellationShape.Of("Ori", Humanes, OrionHigh);

        foreach (var star in shape.Stars)
        {
            var theta = SeparationDegrees(shape.Center, _stars.Locate(star.Star, Humanes, OrionHigh)) * Math.PI / 180;
            var expected = 2 * Math.Tan(theta / 2) * 180 / Math.PI;
            Assert.Equal(expected, Math.Sqrt(star.At.Right * star.At.Right + star.At.Up * star.At.Up), 6);
        }
    }

    [Fact]
    public void NearTheCenter_DistancesAreDegreesOfSky()
    {
        // Betelgeuse to Bellatrix, 7.5° apart: the projection grows by under 1 % this close to the center.
        var shape = ConstellationShape.Of("Ori", Humanes, OrionHigh);
        var betelgeuse = shape.Stars.Single(s => s.Star.ProperName == "Betelgeuse").At;
        var bellatrix = shape.Stars.Single(s => s.Star.ProperName == "Bellatrix").At;
        var apart = SeparationDegrees(
            _stars.Locate(BrightStars.Get("Betelgeuse"), Humanes, OrionHigh), _stars.Locate(BrightStars.Get("Bellatrix"), Humanes, OrionHigh));

        var drawn = Math.Sqrt(Math.Pow(betelgeuse.Right - bellatrix.Right, 2) + Math.Pow(betelgeuse.Up - bellatrix.Up, 2));
        Assert.InRange(drawn / apart, 0.99, 1.03);
    }

    [Theory]
    [InlineData("Ori", "2026-10-09T05:00:00Z")]
    [InlineData("Ori", "2026-10-08T23:00:00Z")] // rising in the east, lying on its side
    [InlineData("Lyr", "2026-10-08T19:00:00Z")] // near the zenith
    [InlineData("UMa", "2026-10-08T19:00:00Z")]
    [InlineData("Peg", "2026-10-08T22:00:00Z")]
    public void Stars_InTheSameDirectionsAsOnTheHud(string symbol, string instant)
    {
        // The HUD pointed at the drawing's center shows each star in the same direction from the middle of the screen.
        var at = DateTimeOffset.Parse(instant);
        var shape = ConstellationShape.Of(symbol, Humanes, at);
        var pointing = new PointingDirection(shape.Center.AzimuthDegrees, shape.Center.AltitudeDegrees);

        foreach (var star in shape.Stars)
        {
            var sky = _stars.Locate(star.Star, Humanes, at);
            var screen = HudProjection.ToScreen(pointing, sky.AzimuthDegrees, sky.AltitudeDegrees, 1000, 2000)!.Value;
            var onHud = Math.Atan2(-(screen.Y - 1000), screen.X - 500) * 180 / Math.PI;
            var onCard = Math.Atan2(star.At.Up, star.At.Right) * 180 / Math.PI;
            Assert.Equal(0, Math.Abs(Math.IEEERemainder(onHud - onCard, 360)), 6);
        }
    }

    [Fact]
    public void UpIsTheZenith_RightIsIncreasingAzimuth()
    {
        // Rising in the east, Orion lies on its side: Betelgeuse (north) to the left and higher than Rigel (south).
        var rising = ConstellationShape.Of("Ori", Humanes, DateTimeOffset.Parse("2026-10-08T23:00:00Z"));
        var betelgeuse = rising.Stars.Single(s => s.Star.ProperName == "Betelgeuse").At;
        var rigel = rising.Stars.Single(s => s.Star.ProperName == "Rigel").At;
        Assert.True(betelgeuse.Right < rigel.Right && betelgeuse.Up > rigel.Up);

        // High in the south it stands up: Betelgeuse above Rigel, still to its left.
        var high = ConstellationShape.Of("Ori", Humanes, OrionHigh);
        betelgeuse = high.Stars.Single(s => s.Star.ProperName == "Betelgeuse").At;
        rigel = high.Stars.Single(s => s.Star.ProperName == "Rigel").At;
        Assert.True(betelgeuse.Up - rigel.Up > 10 && betelgeuse.Right < rigel.Right);
    }

    [Fact]
    public void Center_IsTheMiddleOfTheFigure()
    {
        var shape = ConstellationShape.Of("Ori", Humanes, OrionHigh);

        Assert.InRange(shape.Bounds.MinRight, -20, -5);
        Assert.InRange(shape.Bounds.MaxRight, 5, 20);
        Assert.InRange(shape.Bounds.MinUp, -20, -5);
        Assert.InRange(shape.Bounds.MaxUp, 5, 20);
    }

    [Fact]
    public void Hydra_KeepsItsLengthWithoutStretchingTheEnds()
    {
        // 95.5° from end to end; the center of its smallest cap leaves every vertex within 47.7°, drawn at 2·tan(θ/2): under 51.
        // High in the south, where refraction does not stretch it.
        var shape = ConstellationShape.Of("Hya", Humanes, DateTimeOffset.Parse("2026-10-08T10:00:00Z"));
        var points = shape.Segments.SelectMany(s => new[] { s.From, s.To }).ToList();

        Assert.All(points, p => Assert.InRange(Math.Sqrt(p.Right * p.Right + p.Up * p.Up), 0, 51));
        var length = points.SelectMany(a => points.Select(b => Math.Sqrt(Math.Pow(a.Right - b.Right, 2) + Math.Pow(a.Up - b.Up, 2)))).Max();
        // Its ends grow by 6 %: 101° drawn; the HUD's gnomonic projection would make it 126°.
        Assert.InRange(length, 95.5, 102);
    }

    [Fact]
    public void EveryConstellation_FromThePoleToTheEquator_IsDrawn()
    {
        // Including a figure right at the zenith (the Little Bear from the pole) and the far south from the north.
        foreach (var observer in new[] { Humanes, new Observer(90, 0, 0), new Observer(0, 0, 0), new Observer(-33.87, 151.21, 0) })
        foreach (var figure in ConstellationFigures.All)
        {
            var shape = ConstellationShape.Of(figure.Symbol, observer, OrionHigh);
            Assert.NotEmpty(shape.Segments);
            Assert.All(shape.Segments, s => Assert.True(double.IsFinite(s.From.Right + s.From.Up + s.To.Right + s.To.Up), figure.Symbol));
            Assert.True(shape.Bounds.Width > 0 && shape.Bounds.Height > 0, figure.Symbol);
        }
    }

    [Fact]
    public void Serpens_InItsTwoPieces()
    {
        // 9 and 6 vertices: 8 + 5 segments, none joining the head to the tail across Ophiuchus.
        var shape = ConstellationShape.Of("Ser", Humanes, DateTimeOffset.Parse("2026-06-15T23:00:00Z"));

        Assert.All(shape.Segments, s => Assert.True(s.AboveHorizon));
        Assert.Equal(13, shape.Segments.Count);
        Assert.All(shape.Segments, s => Assert.InRange(Math.Sqrt(Math.Pow(s.From.Right - s.To.Right, 2) + Math.Pow(s.From.Up - s.To.Up, 2)), 0, 12));
    }

    [Fact]
    public void BelowTheHorizon_SplitWhereItCrosses()
    {
        // Orion rising: 24 segments, three of them cut at the horizon.
        var shape = ConstellationShape.Of("Ori", Humanes, DateTimeOffset.Parse("2026-10-08T22:00:00Z"));

        Assert.Equal(27, shape.Segments.Count);
        Assert.Contains(shape.Segments, s => s.AboveHorizon);
        Assert.Contains(shape.Segments, s => !s.AboveHorizon);
        Assert.All(shape.Stars, s => Assert.Equal(_stars.Locate(s.Star, Humanes, DateTimeOffset.Parse("2026-10-08T22:00:00Z")).AltitudeDegrees >= 0, s.AboveHorizon));
        // Each cut is shared by a piece above and a piece below.
        var cuts = shape.Segments.Zip(shape.Segments.Skip(1)).Count(p => p.First.To == p.Second.From && p.First.AboveHorizon != p.Second.AboveHorizon);
        Assert.Equal(3, cuts);
    }

    [Fact]
    public void Stars_InsideTheBoundariesAndOnTheFigure_BrightestFirst()
    {
        var orion = ConstellationShape.Of("Ori", Humanes, OrionHigh);
        Assert.Equal(ConstellationStars.In("Ori"), orion.Stars.Select(s => s.Star));
        Assert.Contains(orion.Stars, s => s.Star.ProperName == "Hatysa"); // in Orion, off the figure

        // Alpheratz, of Andromeda, is a corner of the Square of Pegasus.
        var pegasus = ConstellationShape.Of("Peg", Humanes, OrionHigh);
        Assert.Contains(pegasus.Stars, s => s.Star.ProperName == "Alpheratz");
        Assert.Equal(pegasus.Stars.Select(s => s.Star.Magnitude).Order(), pegasus.Stars.Select(s => s.Star.Magnitude));
        Assert.Equal(["Sabik", "Unukalhai", "Yed Prior"],
            ConstellationShape.Of("Ser", Humanes, OrionHigh).Stars.Select(s => s.Star.Name).Order());
    }

    [Theory]
    [InlineData("Ori", "Rigel", "Betelgeuse", "Bellatrix")]
    [InlineData("Peg", "Enif", "Scheat", "Markab")] // not Alpheratz, brighter but of Andromeda
    [InlineData("UMa", "Alioth", "Dubhe", "Alkaid")]
    public void Labelled_TheThreeBrightestOfItsOwn(string symbol, params string[] names)
    {
        var shape = ConstellationShape.Of(symbol, Humanes, OrionHigh);

        Assert.Equal(names, shape.Stars.Where(s => s.Labelled).Select(s => s.Star.Name));
    }

    [Fact]
    public void FaintVertices_WhereNoStarOfTheCatalogIs()
    {
        // Cancer has no star of magnitude 3: all its vertices are faint, each once.
        var cancer = ConstellationShape.Of("Cnc", Humanes, OrionHigh);
        Assert.Empty(cancer.Stars);
        Assert.Equal(5, cancer.FaintVertices.Count);

        var orion = ConstellationShape.Of("Ori", Humanes, OrionHigh);
        Assert.All(orion.FaintVertices, v => Assert.DoesNotContain(orion.Stars,
            s => Math.Abs(s.At.Right - v.At.Right) < 0.05 && Math.Abs(s.At.Up - v.At.Up) < 0.05));
    }

    /// <summary>
    /// Checked by the user in Stellarium Web (altazimuth mount, Humanes) on 2026-10-08: the line from one star to another in the HUD's
    /// axes (0° towards the zenith, 90° to the right). The values are those of the console the user compared.
    /// </summary>
    [Theory]
    [InlineData("Ori", "2026-10-09T05:00:00Z", "Rigel", "Betelgeuse", 331.3)] // standing in the south
    [InlineData("Ori", "2026-10-09T05:00:00Z", "Rigel", "Bellatrix", 353.7)]
    [InlineData("UMa", "2026-10-08T20:30:00Z", "Alioth", "Dubhe", 98.3)] // low in the north, the handle to the left
    [InlineData("UMa", "2026-10-08T20:30:00Z", "Alioth", "Alkaid", 277.3)]
    [InlineData("Hya", "2026-10-09T05:30:00Z", "Alphard", "gam Hya", 207.6)] // rising in the east, its tail still down
    [InlineData("Ser", "2026-10-08T18:45:00Z", "Sabik", "Unukalhai", 82.6)] // the two pieces, in the south-west
    public void Directions_AsInStellarium(string symbol, string instant, string from, string to, double degrees)
    {
        var shape = ConstellationShape.Of(symbol, Humanes, DateTimeOffset.Parse(instant));
        var a = shape.Stars.Single(s => s.Star.Name == from).At;
        var b = shape.Stars.Single(s => s.Star.Name == to).At;

        var direction = Math.Atan2(b.Right - a.Right, b.Up - a.Up) * 180 / Math.PI;
        Assert.Equal(0, Math.IEEERemainder(direction - degrees, 360), 0.5);
    }

    [Fact]
    public void UnknownSymbol_Throws()
    {
        Assert.Throws<ArgumentException>(() => ConstellationShape.Of("Xyz", Humanes, OrionHigh));
    }

    private static double SeparationDegrees(HorizontalPosition a, HorizontalPosition b)
    {
        static (double, double, double) V(HorizontalPosition p)
        {
            var az = p.AzimuthDegrees * Math.PI / 180;
            var alt = p.AltitudeDegrees * Math.PI / 180;
            return (Math.Sin(az) * Math.Cos(alt), Math.Cos(az) * Math.Cos(alt), Math.Sin(alt));
        }

        var (ax, ay, az) = V(a);
        var (bx, by, bz) = V(b);
        return Math.Acos(Math.Clamp(ax * bx + ay * by + az * bz, -1, 1)) * 180 / Math.PI;
    }
}
