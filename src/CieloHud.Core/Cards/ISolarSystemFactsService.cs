using CieloHud.Core.Sky;
using CieloHud.Core.SolarSystem;

namespace CieloHud.Core.Cards;

/// <summary>Facts of the moment for the cards of the Moon and the planets.</summary>
public interface ISolarSystemFactsService
{
    MoonFacts Moon(Observer observer, DateTimeOffset instant);

    /// <summary>Any <see cref="CelestialBody"/> but the Moon, which has <see cref="Moon"/>.</summary>
    PlanetFacts Planet(CelestialBody planet, Observer observer, DateTimeOffset instant);

    /// <summary>Where Io, Europa, Ganymede and Callisto are next to Jupiter, as the observer sees them.</summary>
    JupiterMoonsFacts JupiterMoons(Observer observer, DateTimeOffset instant);
}
