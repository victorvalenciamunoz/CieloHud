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

    /// <summary>
    /// The moons as seen now: Jupiter's topocentric direction, and the moons where they were when the light left Jupiter
    /// (<c>JupiterMoons</c> is jovicentric and does not correct for light time). Offsets are projected on the plane of the
    /// sky with the observer's horizontal axes, the same "right" and "up" as the HUD. The shadow points away from the Sun.
    /// </summary>
    public JupiterMoonsFacts JupiterMoons(Observer observer, DateTimeOffset instant)
    {
        var time = new AstroTime(instant.UtcDateTime);
        var aeObserver = ToAstronomyEngine(observer);
        const double radiusAu = Astronomy.JUPITER_EQUATORIAL_RADIUS_KM / Astronomy.KM_PER_AU;

        var toJupiter = Astronomy.GeoVector(Body.Jupiter, time, Aberration.Corrected) - Astronomy.ObserverVector(time, aeObserver, EquatorEpoch.J2000);
        var distance = toJupiter.Length();
        var emitted = time.AddDays(-distance / Astronomy.C_AUDAY);
        var moons = Astronomy.JupiterMoons(emitted);
        var sunToJupiter = Unit(Astronomy.HelioVector(Body.Jupiter, emitted));

        // Plane-of-sky axes in EQJ, built in the horizontal frame (x north, y west, z zenith) and rotated back.
        var toHorizontal = Astronomy.Rotation_EQJ_HOR(time, aeObserver);
        var toEquatorial = Astronomy.InverseRotation(toHorizontal);
        var line = Unit(Astronomy.RotateVector(toHorizontal, toJupiter));
        var zenith = new AstroVector(0, 0, 1, time);
        var right = Astronomy.RotateVector(toEquatorial, Unit(Cross(line, zenith)));
        var up = Astronomy.RotateVector(toEquatorial, Unit(Cross(Cross(line, zenith), line)));
        var depth = Astronomy.RotateVector(toEquatorial, line);

        GalileanMoon Moon(GalileanMoonName name, StateVector state)
        {
            var m = new AstroVector(state.x, state.y, state.z, time);
            var along = Dot(m, sunToJupiter);
            var perpendicular = Math.Sqrt(Math.Max(0, Dot(m, m) - along * along));
            var (r, u, d) = (Dot(m, right) / radiusAu, Dot(m, up) / radiusAu, Dot(m, depth) / radiusAu);
            return new GalileanMoon(name, r, u, d,
                GalileanMoons.State(r, u, d, GalileanMoons.InShadow(along / radiusAu, perpendicular / radiusAu)));
        }

        return new JupiterMoonsFacts(
            [
                Moon(GalileanMoonName.Io, moons.io),
                Moon(GalileanMoonName.Europa, moons.europa),
                Moon(GalileanMoonName.Ganymede, moons.ganymede),
                Moon(GalileanMoonName.Callisto, moons.callisto),
            ],
            JupiterRadiusArcseconds: radiusAu / distance * 180 / Math.PI * 3600);
    }

    private static double Dot(AstroVector a, AstroVector b) => a.x * b.x + a.y * b.y + a.z * b.z;

    private static AstroVector Cross(AstroVector a, AstroVector b) =>
        new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x, a.t);

    private static AstroVector Unit(AstroVector v)
    {
        var length = v.Length();
        return new AstroVector(v.x / length, v.y / length, v.z / length, v.t);
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
