using CieloHud.Core.Sky;

namespace CieloHud.Core.Satellites;

/// <summary>
/// Locates Earth satellites in the observer's sky from their orbital elements.
/// </summary>
public interface ISatelliteService
{
    /// <summary>
    /// Horizontal position of the satellite described by <paramref name="tle"/> for <paramref name="observer"/> at <paramref name="instant"/>.
    /// Geometric altitude (no refraction): satellites are only observed well above the horizon, where refraction is negligible.
    /// </summary>
    HorizontalPosition Locate(Tle tle, Observer observer, DateTimeOffset instant);
}
