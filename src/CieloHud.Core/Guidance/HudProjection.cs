namespace CieloHud.Core.Guidance;

/// <summary>A pixel position on the HUD.</summary>
public readonly record struct ScreenPoint(double X, double Y);

/// <summary>Where to draw a target that may be off screen.</summary>
/// <param name="X">Horizontal pixel from the left edge.</param>
/// <param name="Y">Vertical pixel from the top edge.</param>
/// <param name="InView">True when the target falls inside the view (with margin); otherwise X/Y lie on the view edge.</param>
/// <param name="DirectionDegrees">Screen direction from the center to the target: 0° = right, 90° = up, counter-clockwise.</param>
public readonly record struct HudPoint(double X, double Y, bool InView, double DirectionDegrees);

/// <summary>
/// Gnomonic (camera-like) projection of sky directions onto the HUD. The screen is a plane perpendicular to where the
/// phone points, with "up" towards the zenith and "right" towards increasing azimuth. Great circles, such as the horizon
/// or a constellation line, come out straight, and shapes keep their look even when pointing near the zenith.
/// The phone is assumed upright (portrait, no roll compensation).
/// </summary>
public static class HudProjection
{
    /// <summary>Horizontal field of view the HUD pretends to have, close to a phone's main camera.</summary>
    public const double DefaultFieldOfViewDegrees = 60;

    /// <summary>
    /// Pixel position of a sky direction, or null when it lies behind the viewer (90° or more from where the phone points).
    /// The result may fall outside the view; callers drawing lines can let the canvas clip them.
    /// </summary>
    public static ScreenPoint? ToScreen(
        PointingDirection pointing, double azimuthDegrees, double altitudeDegrees,
        double viewWidth, double viewHeight, double fieldOfViewDegrees = DefaultFieldOfViewDegrees)
    {
        Validate(viewWidth, viewHeight, fieldOfViewDegrees);
        var (right, up, forward) = CameraComponents(pointing, azimuthDegrees, altitudeDegrees);
        if (forward <= 1e-6)
            return null;

        var k = PixelsPerUnit(viewWidth, fieldOfViewDegrees);
        return new ScreenPoint(viewWidth / 2 + k * right / forward, viewHeight / 2 - k * up / forward);
    }

    /// <summary>
    /// Like <see cref="ToScreen"/>, but always returns a point: targets outside the view, including behind the viewer,
    /// are pulled onto the edge (inset by <paramref name="edgeMargin"/>) keeping their direction, so an arrow can be drawn there.
    /// </summary>
    public static HudPoint Project(
        PointingDirection pointing, double azimuthDegrees, double altitudeDegrees,
        double viewWidth, double viewHeight,
        double fieldOfViewDegrees = DefaultFieldOfViewDegrees,
        double edgeMargin = 48)
    {
        Validate(viewWidth, viewHeight, fieldOfViewDegrees);
        var (right, up, forward) = CameraComponents(pointing, azimuthDegrees, altitudeDegrees);

        var cx = viewWidth / 2;
        var cy = viewHeight / 2;
        var direction = Math.Abs(right) < 1e-12 && Math.Abs(up) < 1e-12 ? 0 : Math.Atan2(up, right) * 180 / Math.PI;

        var halfW = Math.Max(1, cx - edgeMargin);
        var halfH = Math.Max(1, cy - edgeMargin);

        double dx, dy;
        bool inView;
        if (forward > 1e-6)
        {
            var k = PixelsPerUnit(viewWidth, fieldOfViewDegrees);
            dx = k * right / forward;
            dy = k * up / forward;
            inView = Math.Abs(dx) <= halfW && Math.Abs(dy) <= halfH;
        }
        else
        {
            // Behind the viewer: only the direction matters.
            dx = right;
            dy = up;
            inView = false;
        }

        if (!inView)
        {
            if (Math.Abs(dx) < 1e-12 && Math.Abs(dy) < 1e-12)
                dx = 1; // exactly behind: point right, like the turn hint does for 180°
            var scale = Math.Min(halfW / Math.Max(Math.Abs(dx), 1e-12), halfH / Math.Max(Math.Abs(dy), 1e-12));
            dx *= scale;
            dy *= scale;
        }

        return new HudPoint(cx + dx, cy - dy, inView, direction);
    }

    /// <summary>
    /// Components of the target direction along the camera axes: right, up (towards the zenith) and forward. Also the axes of the
    /// constellation drawings on the cards (<see cref="Cards.ConstellationShape"/>), so card and HUD agree.
    /// </summary>
    internal static (double Right, double Up, double Forward) CameraComponents(PointingDirection pointing, double azimuthDegrees, double altitudeDegrees)
    {
        var az0 = pointing.AzimuthDegrees * Math.PI / 180;
        var alt0 = pointing.AltitudeDegrees * Math.PI / 180;
        var az = azimuthDegrees * Math.PI / 180;
        var alt = altitudeDegrees * Math.PI / 180;

        // East-north-up unit vectors.
        double fx = Math.Sin(az0) * Math.Cos(alt0), fy = Math.Cos(az0) * Math.Cos(alt0), fz = Math.Sin(alt0);
        double rx = Math.Cos(az0), ry = -Math.Sin(az0); // horizontal, towards increasing azimuth; defined even at the zenith
        double ux = ry * fz, uy = -rx * fz, uz = rx * fy - ry * fx; // up = right × forward
        double tx = Math.Sin(az) * Math.Cos(alt), ty = Math.Cos(az) * Math.Cos(alt), tz = Math.Sin(alt);

        return (tx * rx + ty * ry, tx * ux + ty * uy + tz * uz, tx * fx + ty * fy + tz * fz);
    }

    private static double PixelsPerUnit(double viewWidth, double fieldOfViewDegrees) =>
        viewWidth / 2 / Math.Tan(fieldOfViewDegrees / 2 * Math.PI / 180);

    private static void Validate(double viewWidth, double viewHeight, double fieldOfViewDegrees)
    {
        if (viewWidth <= 0 || viewHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewWidth), "View size must be positive.");
        if (fieldOfViewDegrees <= 0 || fieldOfViewDegrees >= 180)
            throw new ArgumentOutOfRangeException(nameof(fieldOfViewDegrees), "Field of view must be between 0 and 180 degrees.");
    }
}
