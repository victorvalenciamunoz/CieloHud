namespace CieloHud.Core.Guidance;

/// <summary>
/// Where the device is pointing, in the same frame as sky positions: azimuth 0° = north, 90° = east; altitude 0° = horizon.
/// </summary>
public readonly record struct PointingDirection
{
    public double AzimuthDegrees { get; }
    public double AltitudeDegrees { get; }

    public PointingDirection(double azimuthDegrees, double altitudeDegrees)
    {
        if (double.IsNaN(altitudeDegrees) || altitudeDegrees < -90 || altitudeDegrees > 90)
            throw new ArgumentOutOfRangeException(nameof(altitudeDegrees), altitudeDegrees, "Altitude must be between -90 and 90 degrees.");

        AzimuthDegrees = Sky.Azimuth.Normalize(azimuthDegrees);
        AltitudeDegrees = altitudeDegrees;
    }
}
