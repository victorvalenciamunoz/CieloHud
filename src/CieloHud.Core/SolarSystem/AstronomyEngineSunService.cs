using CieloHud.Core.Sky;
using CosineKitty;
using Observer = CieloHud.Core.Sky.Observer;

namespace CieloHud.Core.SolarSystem;

/// <summary>
/// <see cref="ISunService"/> backed by Astronomy Engine. No refraction: twilight thresholds are defined on the
/// geometric altitude of the Sun's center (civil -6°, nautical -12°, astronomical -18°).
/// </summary>
public sealed class AstronomyEngineSunService : ISunService
{
    public HorizontalPosition Locate(Observer observer, DateTimeOffset instant) =>
        AstronomyEngineLocator.Locate(Body.Sun, observer, instant, Refraction.None);
}
