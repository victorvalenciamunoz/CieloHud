using CieloHud.Core.Sky;
using CosineKitty;
using Observer = CieloHud.Core.Sky.Observer;

namespace CieloHud.Core.SolarSystem;

/// <summary>
/// <see cref="ISolarSystemService"/> backed by Astronomy Engine (CosineKitty.AstronomyEngine).
/// Returns topocentric, aberration-corrected positions with normal atmospheric refraction,
/// so altitudes match what the eye sees and what Stellarium shows with its atmosphere on.
/// </summary>
public sealed class AstronomyEngineSolarSystemService : ISolarSystemService
{
    public HorizontalPosition Locate(CelestialBody body, Observer observer, DateTimeOffset instant) =>
        AstronomyEngineLocator.Locate(ToAeBody(body), observer, instant, Refraction.Normal);

    private static Body ToAeBody(CelestialBody body) => body switch
    {
        CelestialBody.Moon => Body.Moon,
        CelestialBody.Mercury => Body.Mercury,
        CelestialBody.Venus => Body.Venus,
        CelestialBody.Mars => Body.Mars,
        CelestialBody.Jupiter => Body.Jupiter,
        CelestialBody.Saturn => Body.Saturn,
        _ => throw new ArgumentOutOfRangeException(nameof(body), body, "Unsupported celestial body."),
    };
}
