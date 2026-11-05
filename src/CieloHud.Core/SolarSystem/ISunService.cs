using CieloHud.Core.Sky;

namespace CieloHud.Core.SolarSystem;

/// <summary>
/// Locates the Sun in the observer's sky. Not a target to look at: used to know whether the sky is dark.
/// </summary>
public interface ISunService
{
    /// <summary>
    /// Geometric (unrefracted) horizontal position of the Sun's center for <paramref name="observer"/> at <paramref name="instant"/>,
    /// as twilight definitions require.
    /// </summary>
    HorizontalPosition Locate(Observer observer, DateTimeOffset instant);
}
