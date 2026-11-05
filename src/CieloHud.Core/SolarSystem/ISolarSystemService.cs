using CieloHud.Core.Sky;

namespace CieloHud.Core.SolarSystem;

/// <summary>
/// Locates solar-system bodies in the observer's sky.
/// </summary>
public interface ISolarSystemService
{
    /// <summary>
    /// Apparent (refracted) horizontal position of <paramref name="body"/> for <paramref name="observer"/> at <paramref name="instant"/>.
    /// </summary>
    HorizontalPosition Locate(CelestialBody body, Observer observer, DateTimeOffset instant);
}
