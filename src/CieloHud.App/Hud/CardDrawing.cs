using CieloHud.Core.Cards;
using CieloHud.Core.SolarSystem;

namespace CieloHud.App.Hud;

/// <summary>
/// The drawing on a card (decision 041): flat shapes worked out in Core for this moment, with up towards the zenith and
/// right towards increasing azimuth, as in the sky with the phone upright. Draw only, in the palette, so it turns red at night.
/// </summary>
public sealed class CardDrawing : IDrawable
{
    private const string FontRegular = "ChakraPetch";

    /// <summary>Radius of the Moon's and the inner planets' disc; they are not to scale with each other.</summary>
    private const float PhaseRadius = 30;

    /// <summary>Saturn's equatorial radius; its rings are to scale with it.</summary>
    private const float SaturnRadius = 20;

    /// <summary>
    /// The least the row spans either side, in Jupiter radii: with every moon close in, Jupiter would otherwise fill the card.
    /// The most is Callisto's widest elongation, about 26.3.
    /// </summary>
    private const double JupiterMinRowRadii = 8;
    private const float JupiterMinHeight = 96;
    private const float JupiterMaxHeight = 130;
    private const float MoonDotRadius = 2.5f;

    /// <summary>Inside the binoculars' field, around the row.</summary>
    private const float FieldPadding = 10;

    /// <summary>The moons' names: height of a line, half the longest one ("Ganímedes"), and the least room between two.</summary>
    private const float LabelFontSize = 11;
    private const float LabelSize = 13;
    private const float LabelHalfWidth = 28;
    private const float LabelGap = 4;
    private const float LabelBox = 100;

    /// <summary>Room around the shapes.</summary>
    private const float Margin = 6;

    public CardPicture? Picture { get; set; }

    public HudPalette Palette { get; set; } = HudPalette.Normal;

    /// <summary>The height the drawing needs at this width.</summary>
    public static double HeightFor(CardPicture? picture, double width) => picture switch
    {
        PhasePicture => 2 * (PhaseRadius + Margin),
        StarPicture => 2 * (StarHalo + Margin),
        SaturnPicture saturn => 2 * (SaturnHalfHeight(saturn.Shape) + Margin),
        JupiterPicture jupiter => JupiterLayout(jupiter.Moons, (float)width).Height,
        _ => 0,
    };

    public void Draw(ICanvas canvas, RectF r)
    {
        canvas.SaveState();
        canvas.Antialias = true;
        var center = new PointF(r.Center.X, r.Center.Y);
        switch (Picture)
        {
            case StarPicture star:
                DrawStar(canvas, center, star.Color);
                break;
            case PhasePicture phase:
                DrawPhase(canvas, center, phase.Disc);
                break;
            case SaturnPicture saturn:
                DrawSaturn(canvas, center, saturn.Shape);
                break;
            case JupiterPicture jupiter:
                DrawJupiter(canvas, r, jupiter.Moons);
                break;
        }
        canvas.RestoreState();
    }

    /// <summary>
    /// The color each one shows to the eye (decision 041), in the normal palette only: at night everything is the palette's red.
    /// </summary>
    private static readonly Dictionary<CelestialBody, Color> TrueColors = new()
    {
        [CelestialBody.Moon] = Color.FromArgb("#E4E2DA"),
        [CelestialBody.Mercury] = Color.FromArgb("#C2B8AA"),
        [CelestialBody.Venus] = Color.FromArgb("#F4EAC8"),
        [CelestialBody.Mars] = Color.FromArgb("#E0703C"),
        [CelestialBody.Jupiter] = Color.FromArgb("#EEDFC2"),
        [CelestialBody.Saturn] = Color.FromArgb("#E6CF96"),
    };

    private static readonly Color SaturnRingsColor = Color.FromArgb("#B8A57A");

    /// <summary>A star's point of light and its glow: no disc, the eye sees none.</summary>
    private const float StarCore = 4;
    private const float StarHalo = 20;

    /// <summary>The colors of <see cref="StarColor"/>, toned so they read on the dark card; at night, the palette's red.</summary>
    private static readonly Dictionary<StarColor, Color> StarTrueColors = new()
    {
        [StarColor.Bluish] = Color.FromArgb("#A8C6FF"),
        [StarColor.White] = Color.FromArgb("#F2F5FF"),
        [StarColor.YellowishWhite] = Color.FromArgb("#FFF3D6"),
        [StarColor.Yellowish] = Color.FromArgb("#FFE39A"),
        [StarColor.Orange] = Color.FromArgb("#FFB86E"),
        [StarColor.Reddish] = Color.FromArgb("#FF8A5C"),
    };

    private void DrawStar(ICanvas canvas, PointF center, StarColor color)
    {
        var tint = Palette.TrueColors ? StarTrueColors[color] : Palette.Text;
        Halo(canvas, center, StarCore, StarHalo, tint, 0.55f);
        canvas.FillColor = tint;
        canvas.FillCircle(center, StarCore);
    }

    private Color BodyColor(CelestialBody body) => Palette.TrueColors ? TrueColors[body] : Palette.Text;

    /// <summary>
    /// A glow, the dark side faintly (so a thin crescent still reads as a disc), and the lit part shaded like a sphere: brightest
    /// towards the Sun, darker towards the terminator.
    /// </summary>
    private void DrawPhase(ICanvas canvas, PointF center, BodyDisc disc)
    {
        var color = BodyColor(disc.Body);
        Halo(canvas, center, PhaseRadius, PhaseRadius * 1.4f, color, 0.18f * (float)Math.Max(disc.IlluminatedFraction, 0.3));

        canvas.FillColor = Darker(color, 0.2f);
        canvas.FillCircle(center, PhaseRadius);
        canvas.StrokeColor = color.WithAlpha(0.3f);
        canvas.StrokeSize = 1;
        canvas.DrawCircle(center, PhaseRadius);

        var outline = PhaseShape.LitOutline(disc.IlluminatedFraction, disc.BrightLimbDegrees);
        if (outline.Count == 0)
            return;
        var path = new PathF();
        foreach (var point in outline)
        {
            var screen = ToScreen(center, point, PhaseRadius);
            if (path.Count == 0)
                path.MoveTo(screen);
            else
                path.LineTo(screen);
        }
        path.Close();
        var towardsSun = DiscPoint.Towards(disc.BrightLimbDegrees, 0.35);
        Shaded(canvas, center, PhaseRadius, PhaseRadius, color, (float)towardsSun.Right, (float)towardsSun.Up, c => c.FillPath(path));
    }

    /// <summary>
    /// Fills with a sphere's shading: <paramref name="color"/> around a highlight offset by (<paramref name="right"/>,
    /// <paramref name="up"/>) radii, darkening towards the edge.
    /// </summary>
    private static void Shaded(ICanvas canvas, PointF center, float radiusX, float radiusY, Color color, float right, float up, Action<ICanvas> fill)
    {
        var paint = new RadialGradientPaint
        {
            GradientStops =
            [
                new PaintGradientStop(0, color),
                new PaintGradientStop(0.55f, color),
                new PaintGradientStop(1, Darker(color, 0.5f)),
            ],
            Center = new Point(0.5 + right / 2, 0.5 - up / 2),
            Radius = 0.7,
        };
        canvas.SaveState();
        canvas.SetFillPaint(paint, new RectF(center.X - radiusX, center.Y - radiusY, 2 * radiusX, 2 * radiusY));
        fill(canvas);
        canvas.RestoreState();
    }

    /// <summary>A soft glow around a disc of <paramref name="radius"/>, fading out by <paramref name="halo"/>.</summary>
    private static void Halo(ICanvas canvas, PointF center, float radius, float halo, Color color, float alpha)
    {
        var paint = new RadialGradientPaint
        {
            GradientStops =
            [
                new PaintGradientStop(0, color.WithAlpha(alpha)),
                new PaintGradientStop(radius / halo, color.WithAlpha(alpha)),
                new PaintGradientStop(1, color.WithAlpha(0)),
            ],
        };
        canvas.SaveState();
        canvas.SetFillPaint(paint, new RectF(center.X - halo, center.Y - halo, 2 * halo, 2 * halo));
        canvas.FillCircle(center, halo);
        canvas.RestoreState();
    }

    private static Color Darker(Color color, float factor) =>
        new(color.Red * factor, color.Green * factor, color.Blue * factor, color.Alpha);

    /// <summary>
    /// Drawn with the pole up and then turned: the half of the rings behind the globe, the globe, and the half in front of it,
    /// edged in the background color so it stands out against the globe.
    /// </summary>
    private void DrawSaturn(ICanvas canvas, PointF center, SaturnShape shape)
    {
        var color = BodyColor(CelestialBody.Saturn);
        var ringsColor = Palette.TrueColors ? SaturnRingsColor : Palette.TextMuted;
        Halo(canvas, center, SaturnRadius, SaturnRadius * 1.5f, color, 0.15f);
        canvas.Translate(center.X, center.Y);
        // Positive rotation is clockwise on screen, like the angles of the sky in Core.
        canvas.Rotate((float)shape.PoleDegrees);

        var frontIsNorth = Math.Abs(AngleDifference(shape.FrontDegrees, shape.PoleDegrees)) < 90;
        var outer = (float)(SaturnShape.RingOuterRadius * SaturnRadius);
        var inner = (float)(SaturnShape.RingInnerRadius * SaturnRadius);
        var minor = (float)shape.RingMinorToMajor;
        var rings = new PathF();
        rings.AppendEllipse(-outer, -outer * minor, 2 * outer, 2 * outer * minor);
        rings.AppendEllipse(-inner, -inner * minor, 2 * inner, 2 * inner * minor);

        // Screen y grows downwards: the north half is y < 0.
        void Half(bool north)
        {
            canvas.SaveState();
            canvas.ClipRectangle(-outer - 1, north ? -outer - 1 : 0, 2 * outer + 2, outer + 1);
            canvas.FillPath(rings, WindingMode.EvenOdd);
            canvas.RestoreState();
        }

        canvas.FillColor = ringsColor;
        Half(north: !frontIsNorth);

        // Saturn is never more than 6° from full: the highlight stays in the middle.
        var polar = (float)(shape.GlobePolarRadius * SaturnRadius);
        Shaded(canvas, PointF.Zero, SaturnRadius, polar, color, 0, 0.15f, c => c.FillEllipse(-SaturnRadius, -polar, 2 * SaturnRadius, 2 * polar));

        canvas.SaveState();
        canvas.ClipRectangle(-outer - 1, frontIsNorth ? -outer - 1 : 0, 2 * outer + 2, outer + 1);
        canvas.StrokeColor = Palette.Background;
        canvas.StrokeSize = 2;
        canvas.DrawPath(rings);
        canvas.RestoreState();
        canvas.FillColor = ringsColor;
        Half(north: frontIsNorth);
    }

    /// <summary>
    /// As in binoculars: a dark field, Jupiter at its true size on the row's scale with a soft glow, and the moons that show as
    /// points of light with their names. A name that would touch the previous one on its side goes to the other side of its dot.
    /// </summary>
    private void DrawJupiter(ICanvas canvas, RectF r, JupiterMoonsFacts facts)
    {
        var layout = JupiterLayout(facts, r.Width);
        var center = new PointF(r.Center.X, r.Center.Y);

        // A stretched field: a round one as wide as the card would be as tall as the whole card.
        canvas.FillColor = Colors.Black;
        canvas.FillRoundedRectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2, (r.Height - 2) / 2);
        canvas.StrokeColor = Palette.TextDim;
        canvas.StrokeSize = 1;
        canvas.DrawRoundedRectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2, (r.Height - 2) / 2);

        var jupiterRadius = Math.Max(layout.Scale, 2.5f);
        Glow(canvas, center, jupiterRadius, jupiterRadius * 2.4f, BodyColor(CelestialBody.Jupiter));

        var font = new Microsoft.Maui.Graphics.Font(FontRegular);
        canvas.Font = font;
        canvas.FontSize = LabelFontSize;
        canvas.FontColor = Palette.TextMuted;
        var moons = Visible(facts)
            .Select(m => (Name: FactsText.MoonName(m.Name), At: ToScreen(center, new DiscPoint(m.RightRadii, m.UpRadii), layout.Scale)))
            .OrderBy(m => layout.Upright ? m.At.Y : m.At.X);
        float firstSideEnd = float.MinValue, otherSideEnd = float.MinValue;
        foreach (var (name, at) in moons)
        {
            Glow(canvas, at, MoonDotRadius, MoonDotRadius * 2.6f, Palette.Star);

            // Measured with another font than the one drawn: a margin for collisions, and a wide box so a name never wraps.
            var width = canvas.GetStringSize(name, font, LabelFontSize).Width * 1.2f;
            var (along, halfLength) = layout.Upright ? (at.Y, LabelSize / 2) : (at.X, width / 2);
            var start = along - halfLength;
            var firstSide = start >= firstSideEnd + LabelGap || (start < otherSideEnd + LabelGap && firstSideEnd <= otherSideEnd);
            if (firstSide)
                firstSideEnd = along + halfLength;
            else
                otherSideEnd = along + halfLength;

            const float gap = MoonDotRadius + 3;
            // Beside the dot when the row stands up (right, else left), under it when it lies down (under, else over).
            if (layout.Upright)
                canvas.DrawString(name, firstSide ? at.X + gap : at.X - gap - LabelBox, at.Y - LabelSize / 2, LabelBox, LabelSize,
                    firstSide ? HorizontalAlignment.Left : HorizontalAlignment.Right, VerticalAlignment.Center);
            else
                canvas.DrawString(name, at.X - LabelBox / 2, firstSide ? at.Y + gap : at.Y - gap - LabelSize, LabelBox, LabelSize,
                    HorizontalAlignment.Center, firstSide ? VerticalAlignment.Top : VerticalAlignment.Bottom);
        }
    }

    /// <summary>A point of light: a solid core inside a halo that fades out.</summary>
    private static void Glow(ICanvas canvas, PointF at, float core, float halo, Color color)
    {
        var paint = new RadialGradientPaint { StartColor = color.WithAlpha(0.45f), EndColor = color.WithAlpha(0) };
        // The gradient sticks to later fills on Android: keep it inside its own state.
        canvas.SaveState();
        canvas.SetFillPaint(paint, new RectF(at.X - halo, at.Y - halo, 2 * halo, 2 * halo));
        canvas.FillCircle(at, halo);
        canvas.RestoreState();
        canvas.FillColor = color;
        canvas.FillCircle(at, core);
    }

    private readonly record struct JupiterRow(float Scale, float Height, bool Upright);

    /// <summary>
    /// Points per Jupiter radius: the farthest moon in view fits the field (a fixed scale for Callisto's widest left the
    /// drawing tiny on most days), leaving room for half a name at the ends; when the row stands up (Jupiter low in the east
    /// or west) it shrinks to keep the drawing under <see cref="JupiterMaxHeight"/>. Jupiter stays to scale with its moons.
    /// </summary>
    private static JupiterRow JupiterLayout(JupiterMoonsFacts facts, float width)
    {
        var visible = Visible(facts).ToList();
        var up = visible.Count == 0 ? 1 : Math.Max(1, visible.Max(m => Math.Abs(m.UpRadii)));
        var right = visible.Count == 0 ? 1 : Math.Max(1, visible.Max(m => Math.Abs(m.RightRadii)));
        var span = Math.Max(JupiterMinRowRadii, Math.Max(up, right));
        var aroundRow = LabelSize + MoonDotRadius + 3 + FieldPadding;
        var scale = (float)Math.Min((width / 2 - LabelHalfWidth - FieldPadding) / span, (JupiterMaxHeight / 2 - aroundRow) / up);
        var height = (float)Math.Max(JupiterMinHeight, 2 * (up * scale + aroundRow));
        return new JupiterRow(Math.Max(scale, 0.5f), height, Upright: up > right);
    }

    private static IEnumerable<GalileanMoon> Visible(JupiterMoonsFacts facts) =>
        facts.Moons.Where(m => m.State == GalileanMoonState.Visible);

    /// <summary>How far the rings or the globe reach up or down from the center, once turned.</summary>
    private static float SaturnHalfHeight(SaturnShape shape)
    {
        var a = SaturnShape.RingOuterRadius * SaturnRadius;
        var b = a * shape.RingMinorToMajor;
        // The rings' long axis is square to the pole; the pole's angle from up is the long axis's angle from horizontal.
        var tilt = shape.PoleDegrees * Math.PI / 180;
        var rings = Math.Sqrt(Math.Pow(a * Math.Sin(tilt), 2) + Math.Pow(b * Math.Cos(tilt), 2));
        var globeB = shape.GlobePolarRadius * SaturnRadius;
        var globe = Math.Sqrt(Math.Pow(SaturnRadius * Math.Sin(tilt), 2) + Math.Pow(globeB * Math.Cos(tilt), 2));
        return (float)Math.Max(rings, globe);
    }

    private static PointF ToScreen(PointF center, DiscPoint point, float scale) =>
        new(center.X + (float)(point.Right * scale), center.Y - (float)(point.Up * scale));

    private static double AngleDifference(double a, double b) => ((a - b) % 360 + 540) % 360 - 180;
}
