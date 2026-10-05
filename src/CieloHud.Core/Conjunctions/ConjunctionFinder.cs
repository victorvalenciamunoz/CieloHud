using CieloHud.Core.Guidance;
using CieloHud.Core.Sky;
using CieloHud.Core.SolarSystem;

namespace CieloHud.Core.Conjunctions;

/// <summary>
/// Samples the dark hours every <see cref="ConjunctionCriteria.Step"/> and measures the Moon-planet separation from the
/// observer's apparent positions (decision 025). The Moon's parallax shifts it up to 1°, so the geocentric conjunction
/// is not what the observer sees; Astronomy Engine has no search for that, and the separation changes slowly enough to sample.
/// <para>
/// A window is a run of samples with the sky dark, both bodies above <see cref="ConjunctionCriteria.MinAltitudeDegrees"/>
/// and the separation within <see cref="ConjunctionCriteria.MaxSeparationDegrees"/>. Its best moment is the smallest
/// separation among the samples where both are comfortably high (if any), preferring the evening (local noon to midnight)
/// when the window has an evening part: an hour when people are up.
/// </para>
/// </summary>
public sealed class ConjunctionFinder : IConjunctionFinder
{
    private readonly ISolarSystemService _solarSystem;
    private readonly ISunService _sun;

    public ConjunctionFinder(ISolarSystemService solarSystem, ISunService sun, ConjunctionCriteria? criteria = null)
    {
        _solarSystem = solarSystem;
        _sun = sun;
        Criteria = criteria ?? ConjunctionCriteria.Default;
        Criteria.Validate();
    }

    public ConjunctionCriteria Criteria { get; }

    public IReadOnlyList<MoonPlanetConjunction> Find(Observer observer, DateTimeOffset from, DateTimeOffset to, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var runs = Criteria.Planets.ToDictionary(p => p, _ => new List<ConjunctionPoint>());
        var found = new List<MoonPlanetConjunction>();

        void Close(CelestialBody planet)
        {
            var run = runs[planet];
            if (run.Count == 0)
                return;
            found.Add(ToConjunction(planet, run, timeZone));
            run.Clear();
        }

        // Past "to", keep sampling only to finish the windows already open.
        for (var t = from.ToUniversalTime(); t < to || runs.Values.Any(r => r.Count > 0); t += Criteria.Step)
        {
            var dark = _sun.Locate(observer, t).AltitudeDegrees <= Criteria.MaxSunAltitudeDegrees;
            var moon = dark ? _solarSystem.Locate(CelestialBody.Moon, observer, t) : default;
            var moonUp = dark && moon.AltitudeDegrees >= Criteria.MinAltitudeDegrees;

            foreach (var planet in Criteria.Planets)
            {
                var run = runs[planet];
                if (!moonUp || (t >= to && run.Count == 0))
                {
                    Close(planet);
                    continue;
                }

                var position = _solarSystem.Locate(planet, observer, t);
                var separation = GuidanceCalculator.AngularDistance(
                    moon.AzimuthDegrees, moon.AltitudeDegrees, position.AzimuthDegrees, position.AltitudeDegrees);
                if (position.AltitudeDegrees >= Criteria.MinAltitudeDegrees && separation <= Criteria.MaxSeparationDegrees)
                    run.Add(new ConjunctionPoint(t, moon, position, separation));
                else
                    Close(planet);
            }
        }

        return KeepCloserOfSameApproach(found)
            .OrderBy(c => c.Best.Instant)
            .ThenBy(c => c.Best.SeparationDegrees)
            .ToList();
    }

    private MoonPlanetConjunction ToConjunction(CelestialBody planet, List<ConjunctionPoint> run, TimeZoneInfo timeZone)
    {
        var comfortable = run.Where(p => p.LowerAltitudeDegrees >= Criteria.ComfortableAltitudeDegrees).ToList();
        var pool = comfortable.Count > 0 ? comfortable : run;
        // Dark samples after local noon are the evening part of the night; before it, the small hours.
        var evening = pool.Where(p => TimeZoneInfo.ConvertTime(p.Instant, timeZone).Hour >= 12).ToList();
        var best = (evening.Count > 0 ? evening : pool).MinBy(p => p.SeparationDegrees);
        var closest = run.MinBy(p => p.SeparationDegrees);
        return new MoonPlanetConjunction(planet, run[0], best, closest, run[^1]);
    }

    // Dawn and dusk of the same day can both qualify for one approach: keep the closer, so the user hears about it once.
    private IEnumerable<MoonPlanetConjunction> KeepCloserOfSameApproach(List<MoonPlanetConjunction> found)
    {
        var kept = new List<MoonPlanetConjunction>();
        foreach (var candidate in found.OrderBy(c => c.Best.SeparationDegrees).ThenBy(c => c.Best.Instant))
        {
            var sameApproach = kept.Any(k => k.Planet == candidate.Planet
                && (k.Best.Instant - candidate.Best.Instant).Duration() < Criteria.SameApproachWithin);
            if (!sameApproach)
                kept.Add(candidate);
        }
        return kept;
    }
}
