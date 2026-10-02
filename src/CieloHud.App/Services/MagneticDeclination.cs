using CieloHud.Core.Sky;

namespace CieloHud.App.Services;

/// <summary>
/// Difference between magnetic and true north at the observer, east positive. The rotation-vector sensor is referenced
/// to magnetic north; adding this gives the true azimuth the sky calculations use.
/// </summary>
public static class MagneticDeclination
{
    public static double Degrees(Observer observer, DateTimeOffset instant)
    {
#if ANDROID
        var field = new Android.Hardware.GeomagneticField(
            (float)observer.LatitudeDegrees,
            (float)observer.LongitudeDegrees,
            (float)observer.AltitudeMeters,
            instant.ToUnixTimeMilliseconds());
        return field.Declination;
#else
        return 0;
#endif
    }
}
