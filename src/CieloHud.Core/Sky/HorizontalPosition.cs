namespace CieloHud.Core.Sky;

/// <summary>
/// Where an object appears in the observer's sky.
/// Azimuth: 0° = north, 90° = east, clockwise. Altitude: 0° = horizon, 90° = zenith.
/// </summary>
public readonly record struct HorizontalPosition
{
    /// <summary>Azimuth in degrees, always in [0, 360).</summary>
    public double AzimuthDegrees { get; }

    /// <summary>Apparent altitude above the horizon in degrees, in [-90, 90].</summary>
    public double AltitudeDegrees { get; }

    /// <summary>Distance from the observer in kilometers, when known.</summary>
    public double? DistanceKm { get; }

    public HorizontalPosition(double azimuthDegrees, double altitudeDegrees, double? distanceKm = null)
    {
        if (double.IsNaN(altitudeDegrees) || altitudeDegrees < -90 || altitudeDegrees > 90)
            throw new ArgumentOutOfRangeException(nameof(altitudeDegrees), altitudeDegrees, "Altitude must be between -90 and 90 degrees.");
        if (distanceKm is < 0 || distanceKm is double.NaN)
            throw new ArgumentOutOfRangeException(nameof(distanceKm), distanceKm, "Distance must be zero or positive.");

        AzimuthDegrees = Azimuth.Normalize(azimuthDegrees);
        AltitudeDegrees = altitudeDegrees;
        DistanceKm = distanceKm;
    }

    public CardinalPoint CardinalPoint => Azimuth.ToCardinalPoint(AzimuthDegrees);

    /// <summary>True when the object is geometrically above the horizon. Says nothing about brightness or daylight.</summary>
    public bool IsAboveHorizon => AltitudeDegrees > 0;
}
