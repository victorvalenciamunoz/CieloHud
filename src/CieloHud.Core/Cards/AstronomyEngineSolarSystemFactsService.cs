using CieloHud.Core.SolarSystem;
using CosineKitty;
using AeObserver = CosineKitty.Observer;
using Observer = CieloHud.Core.Sky.Observer;

namespace CieloHud.Core.Cards;

/// <summary>
/// <see cref="ISolarSystemFactsService"/> backed by Astronomy Engine. Distances and the Moon's lit fraction are topocentric,
/// as seen from the observer: the Moon's parallax shifts its lit fraction by up to half a percentage point
/// (<c>Illumination</c> is geocentric). The phase angle and the principal phases are geocentric by convention.
/// </summary>
public sealed class AstronomyEngineSolarSystemFactsService : ISolarSystemFactsService
{
    // Principal phases are 6.6 to 8.2 days apart (measured 2000-2100): searching from 10 days back always finds the previous one.
    private const double QuarterSearchBackDays = 10;

    public MoonFacts Moon(Observer observer, DateTimeOffset instant)
    {
        var time = new AstroTime(instant.UtcDateTime);
        var aeObserver = ToAstronomyEngine(observer);

        var moon = Astronomy.GeoVector(Body.Moon, time, Aberration.Corrected);
        var sun = Astronomy.GeoVector(Body.Sun, time, Aberration.Corrected);
        var here = Astronomy.ObserverVector(time, aeObserver, EquatorEpoch.J2000);
        var moonToSun = sun - moon;
        var moonToObserver = here - moon;
        var phaseAngle = Astronomy.AngleBetween(moonToSun, moonToObserver);
        var distanceKm = moonToObserver.Length() * Astronomy.KM_PER_AU;

        var phase = Astronomy.MoonPhase(time);
        var (previous, next) = SurroundingQuarters(time);

        return new MoonFacts(
            DistanceKm: distanceKm,
            LightTime: LightTravel.Time(distanceKm),
            IlluminatedFraction: (1 + Math.Cos(phaseAngle * Math.PI / 180)) / 2,
            PhaseDegrees: phase,
            Phase: MoonPhases.Name(phase, previous, next, instant),
            Next: next);
    }

    public PlanetFacts Planet(CelestialBody planet, Observer observer, DateTimeOffset instant)
    {
        if (planet == CelestialBody.Moon)
            throw new ArgumentOutOfRangeException(nameof(planet), planet, "The Moon has its own facts.");

        var time = new AstroTime(instant.UtcDateTime);
        var body = AstronomyEngineLocator.ToBody(planet);
        var equatorial = Astronomy.Equator(body, time, ToAstronomyEngine(observer), EquatorEpoch.OfDate, Aberration.Corrected);
        var distanceKm = equatorial.dist * Astronomy.KM_PER_AU;
        return new PlanetFacts(planet, distanceKm, LightTravel.Time(distanceKm));
    }

    /// <summary>The last principal phase at or before <paramref name="time"/> and the first one after it.</summary>
    private static (MoonQuarter Previous, MoonQuarter Next) SurroundingQuarters(AstroTime time)
    {
        var quarter = Astronomy.SearchMoonQuarter(time.AddDays(-QuarterSearchBackDays));
        var previous = quarter;
        while (quarter.time.tt <= time.tt)
        {
            previous = quarter;
            quarter = Astronomy.NextMoonQuarter(quarter);
        }
        return (ToQuarter(previous), ToQuarter(quarter));
    }

    private static MoonQuarter ToQuarter(MoonQuarterInfo info) =>
        new((MoonQuarterKind)info.quarter, new DateTimeOffset(info.time.ToUtcDateTime(), TimeSpan.Zero));

    private static AeObserver ToAstronomyEngine(Observer observer) =>
        new(observer.LatitudeDegrees, observer.LongitudeDegrees, observer.AltitudeMeters);
}
