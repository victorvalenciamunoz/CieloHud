namespace CieloHud.Core.Sky;

/// <summary>
/// Helpers for azimuth angles. Convention: 0° = north, 90° = east, clockwise.
/// </summary>
public static class Azimuth
{
    /// <summary>Wraps any angle into the range [0, 360).</summary>
    public static double Normalize(double degrees)
    {
        if (double.IsNaN(degrees) || double.IsInfinity(degrees))
            throw new ArgumentOutOfRangeException(nameof(degrees), degrees, "Azimuth must be a finite number.");

        var wrapped = degrees % 360;
        if (wrapped < 0)
            wrapped += 360;
        // -1e-15 % 360 yields -1e-15; adding 360 gives exactly 360, which is out of range.
        return wrapped >= 360 ? 0 : wrapped;
    }

    /// <summary>
    /// Nearest of the eight compass points. Each sector spans 45°, centered on the point,
    /// so N covers [337.5, 360) ∪ [0, 22.5) and NE covers [22.5, 67.5).
    /// </summary>
    public static CardinalPoint ToCardinalPoint(double degrees)
    {
        var sector = (int)Math.Floor((Normalize(degrees) + 22.5) / 45) % 8;
        return (CardinalPoint)sector;
    }
}
