namespace CieloHud.Core;

/// <summary>
/// Geographic position of the person looking at the sky.
/// </summary>
public readonly record struct Observer
{
    /// <summary>Geodetic latitude in degrees, positive north.</summary>
    public double LatitudeDegrees { get; }

    /// <summary>Longitude in degrees, positive east.</summary>
    public double LongitudeDegrees { get; }

    /// <summary>Height above sea level in meters.</summary>
    public double AltitudeMeters { get; }

    public Observer(double latitudeDegrees, double longitudeDegrees, double altitudeMeters = 0)
    {
        if (double.IsNaN(latitudeDegrees) || latitudeDegrees < -90 || latitudeDegrees > 90)
            throw new ArgumentOutOfRangeException(nameof(latitudeDegrees), latitudeDegrees, "Latitude must be between -90 and 90 degrees.");
        if (double.IsNaN(longitudeDegrees) || longitudeDegrees < -180 || longitudeDegrees > 180)
            throw new ArgumentOutOfRangeException(nameof(longitudeDegrees), longitudeDegrees, "Longitude must be between -180 and 180 degrees.");
        if (double.IsNaN(altitudeMeters))
            throw new ArgumentOutOfRangeException(nameof(altitudeMeters), altitudeMeters, "Altitude must be a number.");

        LatitudeDegrees = latitudeDegrees;
        LongitudeDegrees = longitudeDegrees;
        AltitudeMeters = altitudeMeters;
    }
}
