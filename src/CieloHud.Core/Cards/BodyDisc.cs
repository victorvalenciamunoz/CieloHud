using CieloHud.Core.SolarSystem;

namespace CieloHud.Core.Cards;

/// <summary>
/// How a body's disc looks right now, for the drawing on its card (decision 041). Directions are angles on the plane of the sky
/// in the HUD's axes: 0° towards the zenith, 90° towards increasing azimuth (right with the phone upright), clockwise like azimuth.
/// </summary>
/// <param name="IlluminatedFraction">Lit part of the disc as seen from the observer, 0 to 1.</param>
/// <param name="BrightLimbDegrees">Where the lit side faces: the direction of the Sun from the body's center.</param>
/// <param name="NorthPoleDegrees">Where the body's north pole (IAU) points.</param>
public sealed record BodyDisc(CelestialBody Body, double IlluminatedFraction, double BrightLimbDegrees, double NorthPoleDegrees);

/// <summary>A point on a body's disc, in disc radii, with the axes of <see cref="BodyDisc"/>: right and up.</summary>
public readonly record struct DiscPoint(double Right, double Up)
{
    /// <summary>The point of the unit circle in <paramref name="degrees"/> (0 up, 90 right), times <paramref name="radius"/>.</summary>
    public static DiscPoint Towards(double degrees, double radius = 1)
    {
        var radians = degrees * Math.PI / 180;
        return new DiscPoint(radius * Math.Sin(radians), radius * Math.Cos(radians));
    }
}
