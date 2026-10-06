namespace CieloHud.Core.SolarSystem;

/// <summary>
/// How bright a solar-system body looks. Lower is brighter: Venus about -4, Jupiter -2, the faintest stars seen from a city about 3.
/// </summary>
public interface IMagnitudeService
{
    /// <summary>Apparent visual magnitude of <paramref name="body"/> at <paramref name="instant"/>, without the dimming of the atmosphere.</summary>
    double Magnitude(CelestialBody body, DateTimeOffset instant);
}
