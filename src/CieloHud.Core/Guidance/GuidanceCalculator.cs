using CieloHud.Core.Sky;

namespace CieloHud.Core.Guidance;

/// <summary>
/// Pure geometry: compares where the device points with where the target is.
/// Stateless; the caller passes the previous on-target state so hysteresis can be applied.
/// </summary>
public sealed class GuidanceCalculator
{
    private readonly GuidanceSettings _settings;

    public GuidanceCalculator(GuidanceSettings? settings = null)
    {
        _settings = settings ?? GuidanceSettings.Default;
        if (_settings.EnterRadiusDegrees <= 0 || _settings.ExitRadiusDegrees < _settings.EnterRadiusDegrees)
            throw new ArgumentException("Exit radius must be at least the enter radius, and both positive.", nameof(settings));
    }

    public Guidance Compute(PointingDirection pointing, HorizontalPosition target, bool wasOnTarget)
    {
        var azimuthDelta = WrapToHalfTurn(target.AzimuthDegrees - pointing.AzimuthDegrees);
        var altitudeDelta = target.AltitudeDegrees - pointing.AltitudeDegrees;
        var distance = AngularDistance(pointing.AzimuthDegrees, pointing.AltitudeDegrees, target.AzimuthDegrees, target.AltitudeDegrees);

        var isOnTarget = wasOnTarget
            ? distance <= _settings.ExitRadiusDegrees
            : distance <= _settings.EnterRadiusDegrees;

        return new Guidance(azimuthDelta, altitudeDelta, distance, isOnTarget);
    }

    /// <summary>Maps any angle into (-180, 180].</summary>
    public static double WrapToHalfTurn(double degrees)
    {
        var wrapped = Azimuth.Normalize(degrees);
        return wrapped > 180 ? wrapped - 360 : wrapped;
    }

    /// <summary>Great-circle angle between two directions given as azimuth/altitude, in degrees.</summary>
    public static double AngularDistance(double azimuth1, double altitude1, double azimuth2, double altitude2)
    {
        var (a1, h1, a2, h2) = (ToRadians(azimuth1), ToRadians(altitude1), ToRadians(azimuth2), ToRadians(altitude2));

        // Haversine on the sphere: altitude plays the role of latitude, azimuth of longitude.
        var sinHalfAltitude = Math.Sin((h2 - h1) / 2);
        var sinHalfAzimuth = Math.Sin((a2 - a1) / 2);
        var h = sinHalfAltitude * sinHalfAltitude + Math.Cos(h1) * Math.Cos(h2) * sinHalfAzimuth * sinHalfAzimuth;
        return ToDegrees(2 * Math.Asin(Math.Sqrt(Math.Clamp(h, 0, 1))));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
    private static double ToDegrees(double radians) => radians * 180 / Math.PI;
}
