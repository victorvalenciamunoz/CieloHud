namespace CieloHud.Core.Cards;

/// <summary>
/// Saturn's globe and rings as seen right now, in Saturn equatorial radii, for its card's drawing (decision 041).
/// Directions in <see cref="BodyDisc"/>'s axes. The rings are one band, from the inner edge of ring B to the outer edge of ring A:
/// the Cassini division, the faint ring C and the globe's shadow are left out of a sketch.
/// </summary>
/// <param name="PoleDegrees">Where Saturn's north pole points on the sky; the rings' long axis is square to it.</param>
/// <param name="GlobePolarRadius">The globe's outline along the pole: an ellipse, flattened by up to 10 % when the rings are open.</param>
/// <param name="RingMinorToMajor">The rings' ellipse, short axis over long axis: the sine of the tilt.</param>
/// <param name="FrontDegrees">The side where the rings pass in front of the globe: away from the face we see.</param>
public sealed record SaturnShape(double PoleDegrees, double GlobePolarRadius, double RingMinorToMajor, double FrontDegrees)
{
    /// <summary>Equatorial radius at 1 bar, the unit (NASA Saturnian Rings Fact Sheet).</summary>
    public const double EquatorialRadiusKm = 60_268;

    /// <summary>Polar radius at 1 bar (NASA Saturn Fact Sheet).</summary>
    public const double PolarRadiusKm = 54_364;

    /// <summary>Inner edge of ring B, 91 975 km (NASA Saturnian Rings Fact Sheet).</summary>
    public const double RingInnerRadius = 91_975 / EquatorialRadiusKm;

    /// <summary>Outer edge of ring A, 136 780 km (NASA Saturnian Rings Fact Sheet).</summary>
    public const double RingOuterRadius = 136_780 / EquatorialRadiusKm;

    /// <param name="tiltDegrees">The rings' tilt from the Earth, positive when the north face shows (<see cref="SaturnRingsFacts"/>).</param>
    /// <param name="northPoleDegrees">Saturn's north pole on the sky (<see cref="BodyDisc.NorthPoleDegrees"/>).</param>
    public static SaturnShape Of(double tiltDegrees, double northPoleDegrees)
    {
        var tilt = tiltDegrees * Math.PI / 180;
        var flattening = PolarRadiusKm / EquatorialRadiusKm;
        var sin = Math.Sin(tilt);
        var cos = Math.Cos(tilt);

        // Seeing the north face, the north pole leans towards us and the near half of the rings drops to the south side.
        var front = tiltDegrees >= 0 ? northPoleDegrees + 180 : northPoleDegrees;
        return new SaturnShape(
            northPoleDegrees,
            GlobePolarRadius: Math.Sqrt(flattening * flattening * cos * cos + sin * sin),
            RingMinorToMajor: Math.Abs(sin),
            FrontDegrees: front % 360);
    }
}
