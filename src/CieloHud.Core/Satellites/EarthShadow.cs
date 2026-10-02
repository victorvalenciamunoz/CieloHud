namespace CieloHud.Core.Satellites;

/// <summary>
/// Geometry of the Earth's shadow. Conical umbra behind a spherical Earth; the Sun is treated as a point at its true distance
/// for the cone angle. A satellite outside the umbra is considered sunlit (the penumbra is a few km wide at ISS altitude,
/// i.e. a second or two of its motion).
/// </summary>
public static class EarthShadow
{
    public const double EarthRadiusKm = 6378.137;
    public const double SunRadiusKm = 695_700;

    /// <summary>True when the satellite is inside the umbra, i.e. it cannot see any part of the Sun's disk.</summary>
    /// <param name="satellite">Satellite position from the Earth's center.</param>
    /// <param name="sun">Sun position from the Earth's center.</param>
    public static bool IsInUmbra(EciPosition satellite, EciPosition sun)
    {
        var sunDistance = sun.Length;
        if (sunDistance <= 0)
            throw new ArgumentException("Sun position must not be the origin.", nameof(sun));

        // Component of the satellite position along the Sun direction (positive = day side).
        var along = satellite.Dot(sun) / sunDistance;
        if (along >= 0)
            return false;

        // Distance from the satellite to the Earth-Sun axis.
        var perpendicular = Math.Sqrt(Math.Max(0, satellite.Dot(satellite) - along * along));

        // The umbra narrows behind the Earth: its radius at distance x along the anti-Sun axis is R - x·tan(θ),
        // where θ is the half-angle of the cone with sin θ = (R_sun - R_earth) / d_sun.
        var tanHalfAngle = Math.Tan(Math.Asin((SunRadiusKm - EarthRadiusKm) / sunDistance));
        var umbraRadius = EarthRadiusKm - (-along) * tanHalfAngle;

        return perpendicular < umbraRadius;
    }
}
