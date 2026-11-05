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
        var (toMoon, moonToSun) = Topocentric(Body.Moon, time, ToAstronomyEngine(observer));
        var distanceKm = toMoon.Length() * Astronomy.KM_PER_AU;

        var phase = Astronomy.MoonPhase(time);
        var (previous, next) = SurroundingQuarters(time);

        return new MoonFacts(
            DistanceKm: distanceKm,
            LightTime: LightTravel.Time(distanceKm),
            IlluminatedFraction: LitFraction(toMoon, moonToSun),
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
        var sky = new SkyPlane(time, aeObserver, toJupiter);

        GalileanMoon Moon(GalileanMoonName name, StateVector state)
        {
            var m = new AstroVector(state.x, state.y, state.z, time);
            var along = Dot(m, sunToJupiter);
            var perpendicular = Math.Sqrt(Math.Max(0, Dot(m, m) - along * along));
            var (r, u, d) = (Dot(m, sky.Right) / radiusAu, Dot(m, sky.Up) / radiusAu, Dot(m, sky.Depth) / radiusAu);
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

    // Saturn takes 29.5 years around the Sun: its rings go from edge-on to widest and back twice, about 7 years each way.
    private const int RingSearchDays = 12 * 366;

    /// <summary>
    /// The tilt from the Earth is the angle between Saturn's pole (IAU WGCCRE 2015, <c>RotationAxis</c>) and the ring plane as seen
    /// along our line of sight. The trend comes from the same angle seen from the Sun, which changes smoothly; searched day by day
    /// for its next widest point or its next crossing of zero.
    /// </summary>
    public SaturnRingsFacts SaturnRings(DateTimeOffset instant)
    {
        var time = new AstroTime(instant.UtcDateTime);
        var tilt = RingLatitude(time, Astronomy.GeoVector(Body.Saturn, time, Aberration.Corrected));

        double FromSun(AstroTime t) => RingLatitude(t, Astronomy.HelioVector(Body.Saturn, t));
        var previous = FromSun(time);
        var opening = Math.Abs(FromSun(time.AddDays(1))) > Math.Abs(previous);
        for (var day = 1; day <= RingSearchDays; day++)
        {
            var next = FromSun(time.AddDays(day));
            var ended = opening ? Math.Abs(next) < Math.Abs(previous) : Math.Sign(next) != Math.Sign(previous);
            if (ended)
                return new SaturnRingsFacts(tilt, opening ? RingTrend.Opening : RingTrend.Closing, instant.AddDays(day - 1), opening ? Math.Abs(previous) : 0);
            previous = next;
        }
        throw new InvalidOperationException("Saturn's rings did not reach their widest or edge-on in 12 years.");
    }

    /// <summary>Latitude over Saturn's ring plane of whoever looks along <paramref name="toSaturn"/>, in degrees.</summary>
    private static double RingLatitude(AstroTime time, AstroVector toSaturn)
    {
        var pole = Astronomy.RotationAxis(Body.Saturn, time).north;
        return Math.Asin(-Dot(toSaturn, pole) / toSaturn.Length()) * 180 / Math.PI;
    }

    /// <summary>
    /// The lit fraction from the observer (decision 036), the Sun's direction from the body's center for the bright limb, and the
    /// IAU north pole, both projected on the plane of the sky with the HUD's axes. Jupiter and Saturn are never less than 99 % lit.
    /// </summary>
    public BodyDisc Disc(CelestialBody body, Observer observer, DateTimeOffset instant)
    {
        var time = new AstroTime(instant.UtcDateTime);
        var aeObserver = ToAstronomyEngine(observer);
        var aeBody = AstronomyEngineLocator.ToBody(body);
        var (toBody, bodyToSun) = Topocentric(aeBody, time, aeObserver);
        var sky = new SkyPlane(time, aeObserver, toBody);
        return new BodyDisc(
            body,
            LitFraction(toBody, bodyToSun),
            BrightLimbDegrees: sky.Direction(bodyToSun),
            NorthPoleDegrees: sky.Direction(Astronomy.RotationAxis(aeBody, time).north));
    }

    /// <summary>
    /// From the observer to the body, and from the body to the Sun, in EQJ (AU). Both from geocentric positions corrected for light
    /// time and aberration: the Sun's own motion while the light travels is far below what a drawing or a percentage shows.
    /// </summary>
    private static (AstroVector ToBody, AstroVector BodyToSun) Topocentric(Body body, AstroTime time, AeObserver observer)
    {
        var target = Astronomy.GeoVector(body, time, Aberration.Corrected);
        var sun = Astronomy.GeoVector(Body.Sun, time, Aberration.Corrected);
        var here = Astronomy.ObserverVector(time, observer, EquatorEpoch.J2000);
        return (target - here, sun - target);
    }

    /// <summary>Lit part of the disc, from the phase angle between the body's directions to the Sun and to the observer.</summary>
    private static double LitFraction(AstroVector toBody, AstroVector bodyToSun)
    {
        var phaseAngle = Astronomy.AngleBetween(bodyToSun, new AstroVector(-toBody.x, -toBody.y, -toBody.z, toBody.t));
        return (1 + Math.Cos(phaseAngle * Math.PI / 180)) / 2;
    }

    /// <summary>
    /// The plane of the sky around a body, with the HUD's axes in EQJ: right (towards increasing azimuth), up (towards the zenith)
    /// and depth (along the line of sight, away from the observer). Built in the horizontal frame (x north, y west, z zenith).
    /// </summary>
    private readonly struct SkyPlane
    {
        public SkyPlane(AstroTime time, AeObserver observer, AstroVector toBody)
        {
            var toHorizontal = Astronomy.Rotation_EQJ_HOR(time, observer);
            var toEquatorial = Astronomy.InverseRotation(toHorizontal);
            var line = Unit(Astronomy.RotateVector(toHorizontal, toBody));
            var zenith = new AstroVector(0, 0, 1, time);
            Right = Astronomy.RotateVector(toEquatorial, Unit(Cross(line, zenith)));
            Up = Astronomy.RotateVector(toEquatorial, Unit(Cross(Cross(line, zenith), line)));
            Depth = Astronomy.RotateVector(toEquatorial, line);
        }

        public AstroVector Right { get; }
        public AstroVector Up { get; }
        public AstroVector Depth { get; }

        /// <summary>Where <paramref name="v"/> (EQJ) points on the plane of the sky: 0° up, 90° right, 0 to 360.</summary>
        public double Direction(AstroVector v)
        {
            var degrees = Math.Atan2(Dot(v, Right), Dot(v, Up)) * 180 / Math.PI;
            return degrees < 0 ? degrees + 360 : degrees;
        }
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
