using CieloHud.Core.Constellations;
using CieloHud.Core.Guidance;
using CieloHud.Core.Sky;
using CieloHud.Core.Stars;

namespace CieloHud.Core.Cards;

/// <summary>
/// A point on a constellation's drawing, in degrees of sky at its center: right towards increasing azimuth and up towards the
/// zenith, as on the HUD with the phone upright.
/// </summary>
public readonly record struct ChartPoint(double Right, double Up);

/// <summary>A straight piece of the figure, wholly above or wholly below the horizon.</summary>
public readonly record struct ChartSegment(ChartPoint From, ChartPoint To, bool AboveHorizon);

/// <summary>A star of the catalog on the drawing.</summary>
/// <param name="Labelled">One of the three brightest of the constellation (the ones its card names), which get their name.</param>
public sealed record ChartStar(Star Star, ChartPoint At, bool AboveHorizon, bool Labelled);

/// <summary>A vertex of the figure with no star of the catalog: a star fainter than magnitude 3.</summary>
public readonly record struct ChartVertex(ChartPoint At, bool AboveHorizon);

/// <summary>
/// A constellation as it looks right now, for its card's drawing (decision 049): its stick figure (d3-celestial, decision 017) and its
/// stars, in the HUD's axes and in a stereographic projection centred on the figure, which keeps the shape of the large ones (the
/// Hydra spans 95°) where the HUD's gnomonic projection would stretch their ends.
/// </summary>
/// <param name="Center">Where the drawing is centred: the middle of the smallest cap of sky holding the figure, at (0, 0).</param>
/// <param name="Segments">The figure, split where it crosses the horizon. Serpens stays in its two pieces.</param>
/// <param name="Stars">The catalog's stars inside the boundaries, and those of a neighbour on a vertex of the figure (Alpheratz in
/// Pegasus), brightest first.</param>
/// <param name="FaintVertices">The other vertices of the figure, once each.</param>
/// <param name="Bounds">The smallest box holding all of the above.</param>
public sealed record ConstellationShape(
    string Symbol, HorizontalPosition Center, IReadOnlyList<ChartSegment> Segments, IReadOnlyList<ChartStar> Stars,
    IReadOnlyList<ChartVertex> FaintVertices, ChartBounds Bounds)
{
    /// <summary>How close a star of the catalog must be to a vertex to be the star of that vertex; the figures pass within 0.02°.</summary>
    public const double VertexMatchDegrees = 0.05;

    /// <summary>How many stars get their name: the ones the card's facts list (<see cref="ConstellationFacts"/>).</summary>
    public const int LabelledStars = 3;

    private static readonly Dictionary<string, Layout> Layouts = new(StringComparer.Ordinal);

    /// <summary>The drawing of the constellation <paramref name="symbol"/> (IAU) for the observer at that instant.</summary>
    public static ConstellationShape Of(string symbol, Observer observer, DateTimeOffset instant)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        var layout = LayoutOf(symbol);

        // One batch: the center, the vertices and the stars share the precession matrix.
        var directions = new List<(double Ra, double Dec)> { layout.Center };
        directions.AddRange(layout.Lines.SelectMany(line => line.Select(p => (p.RightAscensionDegrees, p.DeclinationDegrees))));
        directions.AddRange(layout.Stars.Select(s => (s.RightAscensionDegrees, s.DeclinationDegrees)));
        var sky = J2000Sky.LocateAll(directions, observer, instant);

        var center = new PointingDirection(sky[0].AzimuthDegrees, sky[0].AltitudeDegrees);
        ChartPoint Project(HorizontalPosition p) => Stereographic(center, p);
        var index = 1;

        var segments = new List<ChartSegment>();
        var faint = new List<ChartVertex>();
        foreach (var line in layout.Lines)
        {
            var positions = sky.Skip(index).Take(line.Count).ToList();
            for (var i = 0; i < line.Count; i++)
            {
                if (i > 0)
                    segments.AddRange(Split(positions[i - 1], positions[i], Project));
                if (layout.FaintVertices.Contains(line[i]))
                    faint.Add(new ChartVertex(Project(positions[i]), positions[i].AltitudeDegrees >= 0));
            }
            index += line.Count;
        }

        var stars = layout.Stars
            .Select((star, i) => new ChartStar(star, Project(sky[index + i]), sky[index + i].AltitudeDegrees >= 0, layout.Labelled.Contains(star)))
            .ToList();

        // A vertex shared by two lines of the figure gets one dot.
        faint = faint.DistinctBy(v => (Math.Round(v.At.Right, 6), Math.Round(v.At.Up, 6))).ToList();
        var points = segments.SelectMany(s => new[] { s.From, s.To }).Concat(stars.Select(s => s.At)).Concat(faint.Select(v => v.At)).ToList();
        var bounds = new ChartBounds(points.Min(p => p.Right), points.Max(p => p.Right), points.Min(p => p.Up), points.Max(p => p.Up));
        return new ConstellationShape(symbol, sky[0], segments, stars, faint, bounds);
    }

    /// <summary>
    /// Stereographic projection centred on <paramref name="center"/>, with the HUD's camera axes: the sky's angles and the shape of
    /// small patches are kept (a 48° radius is drawn 1.2 times larger at the edge, the same in every direction). Degrees at the center.
    /// </summary>
    internal static ChartPoint Stereographic(PointingDirection center, HorizontalPosition position)
    {
        var (right, up, forward) = HudProjection.CameraComponents(center, position.AzimuthDegrees, position.AltitudeDegrees);
        var k = 2 / (1 + forward) * 180 / Math.PI;
        return new ChartPoint(k * right, k * up);
    }

    /// <summary>A segment of the figure, cut where it crosses the horizon (linearly in altitude: the pieces are a few degrees long).</summary>
    private static IEnumerable<ChartSegment> Split(HorizontalPosition a, HorizontalPosition b, Func<HorizontalPosition, ChartPoint> project)
    {
        var from = project(a);
        var to = project(b);
        var aboveA = a.AltitudeDegrees >= 0;
        var aboveB = b.AltitudeDegrees >= 0;
        if (aboveA == aboveB)
        {
            yield return new ChartSegment(from, to, aboveA);
            yield break;
        }

        var t = a.AltitudeDegrees / (a.AltitudeDegrees - b.AltitudeDegrees);
        var cut = new ChartPoint(from.Right + t * (to.Right - from.Right), from.Up + t * (to.Up - from.Up));
        yield return new ChartSegment(from, cut, aboveA);
        yield return new ChartSegment(cut, to, aboveB);
    }

    /// <summary>What does not change with the moment: the figure, its stars and its center, worked out once per constellation.</summary>
    private sealed record Layout(
        IReadOnlyList<IReadOnlyList<EquatorialPoint>> Lines, (double Ra, double Dec) Center, IReadOnlyList<Star> Stars,
        IReadOnlySet<Star> Labelled, IReadOnlySet<EquatorialPoint> FaintVertices);

    private static Layout LayoutOf(string symbol)
    {
        lock (Layouts)
        {
            if (!Layouts.TryGetValue(symbol, out var layout))
                Layouts[symbol] = layout = BuildLayout(symbol);
            return layout;
        }
    }

    private static Layout BuildLayout(string symbol)
    {
        var figure = ConstellationFigures.Get(symbol) ?? throw new ArgumentException($"Unknown constellation '{symbol}'.", nameof(symbol));
        var vertices = figure.Lines.SelectMany(line => line).ToList();
        var inside = ConstellationStars.In(symbol);

        bool OnVertex(Star star, EquatorialPoint vertex) =>
            Separation(star.RightAscensionDegrees, star.DeclinationDegrees, vertex.RightAscensionDegrees, vertex.DeclinationDegrees) < VertexMatchDegrees;

        var neighbours = BrightStars.All.Where(s => !inside.Contains(s) && vertices.Any(v => OnVertex(s, v)));
        var stars = inside.Concat(neighbours).OrderBy(s => s.Magnitude).ToList();
        var faint = vertices.Where(v => !stars.Any(s => OnVertex(s, v))).ToHashSet();

        var points = vertices.Select(v => Unit(v.RightAscensionDegrees, v.DeclinationDegrees))
            .Concat(stars.Select(s => Unit(s.RightAscensionDegrees, s.DeclinationDegrees)))
            .ToList();
        var center = EnclosingCapCenter(points);
        var centerRa = Math.Atan2(center.Y, center.X) * 180 / Math.PI;
        var centerDec = Math.Asin(Math.Clamp(center.Z, -1, 1)) * 180 / Math.PI;

        return new Layout(figure.Lines, ((centerRa + 360) % 360, centerDec), stars, inside.Take(LabelledStars).ToHashSet(), faint);
    }

    /// <summary>
    /// The center of the smallest cap of sky holding all the points, near enough for a drawing (Bădoiu–Clarkson: step towards the
    /// farthest point by less each time). For the Hydra, 47.7° of radius; the mean of its vertices would leave its far end at 67.6°.
    /// </summary>
    internal static (double X, double Y, double Z) EnclosingCapCenter(IReadOnlyList<(double X, double Y, double Z)> points)
    {
        var c = Normalize(points.Aggregate((0.0, 0.0, 0.0), (s, p) => (s.Item1 + p.X, s.Item2 + p.Y, s.Item3 + p.Z)));
        for (var i = 1; i <= 2000; i++)
        {
            var far = points.MinBy(p => Dot(c, p));
            var step = 1.0 / (i + 1);
            c = Normalize((c.X + (far.X - c.X) * step, c.Y + (far.Y - c.Y) * step, c.Z + (far.Z - c.Z) * step));
        }
        return c;
    }

    internal static double Separation(double ra1, double dec1, double ra2, double dec2) =>
        Math.Acos(Math.Clamp(Dot(Unit(ra1, dec1), Unit(ra2, dec2)), -1, 1)) * 180 / Math.PI;

    internal static (double X, double Y, double Z) Unit(double raDegrees, double decDegrees)
    {
        var ra = raDegrees * Math.PI / 180;
        var dec = decDegrees * Math.PI / 180;
        return (Math.Cos(dec) * Math.Cos(ra), Math.Cos(dec) * Math.Sin(ra), Math.Sin(dec));
    }

    private static double Dot((double X, double Y, double Z) a, (double X, double Y, double Z) b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    private static (double X, double Y, double Z) Normalize((double X, double Y, double Z) v)
    {
        var n = Math.Sqrt(Dot(v, v));
        return (v.X / n, v.Y / n, v.Z / n);
    }
}

/// <summary>The extent of a drawing, in the degrees of <see cref="ChartPoint"/>.</summary>
public readonly record struct ChartBounds(double MinRight, double MaxRight, double MinUp, double MaxUp)
{
    public double Width => MaxRight - MinRight;
    public double Height => MaxUp - MinUp;
}
