using CosineKitty;

namespace CieloHud.Core.SolarSystem;

/// <summary>
/// <see cref="IMagnitudeService"/> backed by Astronomy Engine's <c>Illumination</c>. Geocentric: from anywhere on Earth the
/// difference is negligible.
/// </summary>
public sealed class AstronomyEngineMagnitudeService : IMagnitudeService
{
    public double Magnitude(CelestialBody body, DateTimeOffset instant) =>
        Astronomy.Illumination(AstronomyEngineLocator.ToBody(body), new AstroTime(instant.UtcDateTime)).mag;
}
