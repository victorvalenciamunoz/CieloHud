using CieloHud.Core.Passes;
using CieloHud.Core.Satellites;
using CieloHud.Core.Sky;

namespace CieloHud.Core.Cards;

/// <summary>Whether the observer can see the satellite now, by the rules of a visible pass (decision 043).</summary>
public enum SatelliteSight
{
    /// <summary>Lit by the Sun against a dark enough sky.</summary>
    Visible,

    /// <summary>Lit, but the Sun is too high: the sky outshines it.</summary>
    SkyTooBright,

    /// <summary>In the Earth's shadow: nothing lights it.</summary>
    InEarthShadow,

    /// <summary>Under the horizon: it set while the card was open (the HUD does not guide below it).</summary>
    BelowHorizon,
}

/// <summary>A satellite right now, for its card.</summary>
/// <param name="AltitudeKm">Height over the Earth's ellipsoid, not over a round Earth (up to 21 km apart).</param>
/// <param name="DistanceKm">From the observer.</param>
/// <param name="SpeedKmPerSecond">Around the Earth's center, the figure NASA gives (the ground turns under it at up to 0.4 km/s).</param>
/// <param name="Period">One turn around the Earth.</param>
public sealed record SatelliteFacts(double AltitudeKm, double DistanceKm, double SpeedKmPerSecond, TimeSpan Period, SatelliteSight Sight);

/// <summary>Facts of the moment for the ISS card.</summary>
public interface ISatelliteFactsService
{
    SatelliteFacts Satellite(Tle tle, Observer observer, DateTimeOffset instant);
}

/// <summary>The rule of <see cref="SatelliteSight"/>, apart so it can be tested on its own.</summary>
public static class SatelliteSights
{
    /// <summary>
    /// Under the horizon nothing else matters; in the shadow it cannot be seen whatever the sky; lit, the Sun must be below the
    /// visible-pass limit (−6°).
    /// </summary>
    public static SatelliteSight Of(double altitudeDegrees, bool sunlit, double sunAltitudeDegrees, VisibilityCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        if (altitudeDegrees < 0)
            return SatelliteSight.BelowHorizon;
        if (!sunlit)
            return SatelliteSight.InEarthShadow;
        return sunAltitudeDegrees < criteria.MaxSunAltitudeDegrees ? SatelliteSight.Visible : SatelliteSight.SkyTooBright;
    }
}
