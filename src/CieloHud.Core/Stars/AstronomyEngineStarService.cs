using CieloHud.Core.Sky;

namespace CieloHud.Core.Stars;

/// <summary>
/// <see cref="IStarService"/> backed by Astronomy Engine: J2000 catalog direction rotated to the true equator of date
/// (precession + nutation), then to the observer's horizon with normal refraction, like the planets. See <see cref="J2000Sky"/>.
/// </summary>
public sealed class AstronomyEngineStarService : IStarService
{
    public HorizontalPosition Locate(Star star, Observer observer, DateTimeOffset instant) =>
        J2000Sky.Locate(star.RightAscensionDegrees, star.DeclinationDegrees, observer, instant);
}
