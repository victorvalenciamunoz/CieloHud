namespace CieloHud.Core.Guidance;

/// <summary>Where to draw the target on screen.</summary>
/// <param name="X">Horizontal pixel from the left edge.</param>
/// <param name="Y">Vertical pixel from the top edge.</param>
/// <param name="InView">True when the target falls inside the view (with margin); otherwise X/Y lie on the view edge.</param>
/// <param name="DirectionDegrees">Screen direction from the center to the target: 0° = right, 90° = up, counter-clockwise.</param>
public readonly record struct HudPoint(double X, double Y, bool InView, double DirectionDegrees);

/// <summary>
/// Maps a <see cref="Guidance"/> onto a flat screen: the reticle sits at the center and the target is offset by its
/// angular distance at a fixed scale (degrees per pixel from the horizontal field of view). Azimuth offsets shrink with
/// the cosine of the pointing altitude, as they do on the sky. Targets beyond the view are clamped to the edge so an
/// arrow can be drawn there.
/// </summary>
public static class HudProjection
{
    public const double DefaultFieldOfViewDegrees = 60;

    public static HudPoint Project(
        Guidance guidance,
        double pointingAltitudeDegrees,
        double viewWidth,
        double viewHeight,
        double fieldOfViewDegrees = DefaultFieldOfViewDegrees,
        double edgeMargin = 48)
    {
        if (viewWidth <= 0 || viewHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewWidth), "View size must be positive.");
        if (fieldOfViewDegrees <= 0)
            throw new ArgumentOutOfRangeException(nameof(fieldOfViewDegrees), "Field of view must be positive.");

        var pixelsPerDegree = viewWidth / fieldOfViewDegrees;
        var foreshortening = Math.Max(0.1, Math.Cos(pointingAltitudeDegrees * Math.PI / 180));
        var dx = guidance.AzimuthDeltaDegrees * foreshortening * pixelsPerDegree;
        var dy = guidance.AltitudeDeltaDegrees * pixelsPerDegree;

        var cx = viewWidth / 2;
        var cy = viewHeight / 2;
        var direction = dx == 0 && dy == 0 ? 0 : Math.Atan2(dy, dx) * 180 / Math.PI;

        var halfW = Math.Max(1, cx - edgeMargin);
        var halfH = Math.Max(1, cy - edgeMargin);
        var inView = Math.Abs(dx) <= halfW && Math.Abs(dy) <= halfH;
        if (!inView)
        {
            // Scale the offset back onto the inset rectangle, keeping its direction.
            var scale = Math.Min(halfW / Math.Abs(dx == 0 ? double.Epsilon : dx), halfH / Math.Abs(dy == 0 ? double.Epsilon : dy));
            dx *= scale;
            dy *= scale;
        }

        return new HudPoint(cx + dx, cy - dy, inView, direction);
    }
}
