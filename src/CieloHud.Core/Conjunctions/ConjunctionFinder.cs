using CieloHud.Core.Guidance;
using CieloHud.Core.Sky;
using CieloHud.Core.SolarSystem;

namespace CieloHud.Core.Conjunctions;

/// <summary>
/// Samples the dark hours every <see cref="ConjunctionCriteria.Step"/> and measures the separation of two bodies from the
/// observer's apparent positions (decisions 025 and 028). The Moon's parallax shifts it up to 1°, so the geocentric conjunction
/// is not what the observer sees; Astronomy Engine has no search for that, and the separation changes slowly enough to sample.
/// <para>
/// A window is a run of samples with the sky dark, both bodies above <see cref="ConjunctionCriteria.MinAltitudeDegrees"/>
/// and the separation within the limit. Its best moment prefers the evening (local noon to midnight) when the window has an
/// evening part: an hour when people are up. With the Moon, it is the smallest separation among the samples where both are
/// comfortably high (if any). Two planets barely move against each other in a night (hundredths of a degree an hour), so for
/// them it is the moment when the lower of the two is highest.
/// </para>
/// <para>
/// Two planets stay close for many nights: those nights form one approach, and only its closest night is a conjunction.
/// </para>
/// </summary>
public sealed class ConjunctionFinder : IConjunctionFinder
{
    /// <summary>
    /// Usual order of brightness, to choose which of two planets the HUD guides to: Venus (about -4), Jupiter (-2),
    /// Mars (from -2.9 at opposition to +1.6), Saturn (about +0.5), Mercury. Mars can outshine Jupiter for a few weeks every
    /// two years; it is still right next to it.
    /// </summary>
    private static readonly CelestialBody[] ByBrightness =
        [CelestialBody.Venus, CelestialBody.Jupiter, CelestialBody.Mars, CelestialBody.Saturn, CelestialBody.Mercury];

    /// <summary>
    /// Two bright planets drift apart by under 2° a day, so a pair further than its limit plus this at noon cannot be within
    /// the limit that night: only the nights that pass this daily check are sampled.
    /// </summary>
    private const double DailyCheckMarginDegrees = 2;

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

    public IReadOnlyList<Conjunction> FindWithMoon(Observer observer, DateTimeOffset from, DateTimeOffset to, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var pairs = Criteria.Planets.Select(p => (CelestialBody.Moon, p)).ToList();
        var found = Windows(observer, from, to, pairs, Criteria.MaxMoonSeparationDegrees, timeZone);
        return KeepCloserOfSameApproach(found).OrderBy(c => c.Best.Instant).ThenBy(c => c.Best.SeparationDegrees).ToList();
    }

    public IReadOnlyList<Conjunction> FindPlanetPairs(Observer observer, DateTimeOffset from, DateTimeOffset to, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var maxSeparation = Criteria.MaxPlanetSeparationDegrees;
        // The whole approach is needed to know which night is the closest.
        var searchFrom = (from - Criteria.PlanetApproachSearch).ToUniversalTime();
        var searchTo = (to + Criteria.PlanetApproachSearch).ToUniversalTime();

        var result = new List<Conjunction>();
        foreach (var pair in PlanetPairs())
        {
            var windows = new List<Conjunction>();
            foreach (var (runFrom, runTo) in CloseDays(observer, pair, searchFrom, searchTo, maxSeparation + DailyCheckMarginDegrees))
                windows.AddRange(Windows(observer, runFrom, runTo, [pair], maxSeparation, timeZone, bestByHeight: true));

            foreach (var approach in ConsecutiveNights(windows))
            {
                var closest = approach.MinBy(c => c.Best.SeparationDegrees)!;
                if (closest.End.Instant >= from && closest.Start.Instant < to)
                    result.Add(closest with { Nights = new ConjunctionNights(approach[0].Start.Instant, approach[^1].Start.Instant) });
            }
        }
        return result.OrderBy(c => c.Best.Instant).ToList();
    }

    // Every pair of planets, the brighter one as the guide.
    private IEnumerable<(CelestialBody Guide, CelestialBody Companion)> PlanetPairs()
    {
        var planets = Criteria.Planets.OrderBy(p => Array.IndexOf(ByBrightness, p)).ToList();
        for (var i = 0; i < planets.Count; i++)
            for (var j = i + 1; j < planets.Count; j++)
                yield return (planets[i], planets[j]);
    }

    // Runs of days whose noon separation is within reach: [noon, next noon) spans one whole night.
    private IEnumerable<(DateTimeOffset From, DateTimeOffset To)> CloseDays(
        Observer observer, (CelestialBody Guide, CelestialBody Companion) pair, DateTimeOffset from, DateTimeOffset to, double reach)
    {
        DateTimeOffset? runStart = null;
        var noon = new DateTimeOffset(from.UtcDateTime.Date.AddHours(12), TimeSpan.Zero);
        for (; noon < to; noon = noon.AddDays(1))
        {
            var close = Separation(_solarSystem.Locate(pair.Guide, observer, noon), _solarSystem.Locate(pair.Companion, observer, noon)) <= reach;
            if (close && runStart is null)
                runStart = noon;
            else if (!close && runStart is { } start)
            {
                yield return (start, noon);
                runStart = null;
            }
        }
        if (runStart is { } open)
            yield return (open, noon);
    }

    // Windows on consecutive nights (starts less than a day and a half apart) are one approach.
    private static IEnumerable<List<Conjunction>> ConsecutiveNights(List<Conjunction> windows)
    {
        var group = new List<Conjunction>();
        foreach (var window in windows.OrderBy(w => w.Start.Instant))
        {
            if (group.Count > 0 && window.Start.Instant - group[^1].Start.Instant > TimeSpan.FromDays(1.5))
            {
                yield return group;
                group = [];
            }
            group.Add(window);
        }
        if (group.Count > 0)
            yield return group;
    }

    /// <summary>Every window, for each pair, whose start lies in [from, to); one still open at <paramref name="to"/> runs to its end.</summary>
    private List<Conjunction> Windows(Observer observer, DateTimeOffset from, DateTimeOffset to,
        IReadOnlyList<(CelestialBody Guide, CelestialBody Companion)> pairs, double maxSeparation, TimeZoneInfo timeZone,
        bool bestByHeight = false)
    {
        var runs = pairs.ToDictionary(p => p, _ => new List<ConjunctionPoint>());
        var found = new List<Conjunction>();

        void Close((CelestialBody, CelestialBody) pair)
        {
            var run = runs[pair];
            if (run.Count == 0)
                return;
            found.Add(ToConjunction(pair, run, timeZone, bestByHeight));
            run.Clear();
        }

        // Past "to", keep sampling only to finish the windows already open.
        for (var t = from.ToUniversalTime(); t < to || runs.Values.Any(r => r.Count > 0); t += Criteria.Step)
        {
            var dark = _sun.Locate(observer, t).AltitudeDegrees <= Criteria.MaxSunAltitudeDegrees;
            var positions = new Dictionary<CelestialBody, HorizontalPosition>();
            HorizontalPosition At(CelestialBody body) =>
                positions.TryGetValue(body, out var p) ? p : positions[body] = _solarSystem.Locate(body, observer, t);

            foreach (var pair in pairs)
            {
                var run = runs[pair];
                if (!dark || (t >= to && run.Count == 0))
                {
                    Close(pair);
                    continue;
                }

                var guide = At(pair.Guide);
                if (guide.AltitudeDegrees < Criteria.MinAltitudeDegrees)
                {
                    Close(pair);
                    continue;
                }

                var companion = At(pair.Companion);
                var separation = Separation(guide, companion);
                if (companion.AltitudeDegrees >= Criteria.MinAltitudeDegrees && separation <= maxSeparation)
                    run.Add(new ConjunctionPoint(t, guide, companion, separation));
                else
                    Close(pair);
            }
        }
        return found;
    }

    private Conjunction ToConjunction(
        (CelestialBody Guide, CelestialBody Companion) pair, List<ConjunctionPoint> run, TimeZoneInfo timeZone, bool bestByHeight)
    {
        var comfortable = run.Where(p => p.LowerAltitudeDegrees >= Criteria.ComfortableAltitudeDegrees).ToList();
        var pool = comfortable.Count > 0 && !bestByHeight ? comfortable : run;
        // Dark samples after local noon are the evening part of the night; before it, the small hours.
        var evening = pool.Where(p => TimeZoneInfo.ConvertTime(p.Instant, timeZone).Hour >= 12).ToList();
        var candidates = evening.Count > 0 ? evening : pool;
        var best = bestByHeight
            ? candidates.MaxBy(p => p.LowerAltitudeDegrees)
            : candidates.MinBy(p => p.SeparationDegrees);
        var closest = run.MinBy(p => p.SeparationDegrees);
        return new Conjunction(pair.Guide, pair.Companion, run[0], best, closest, run[^1]);
    }

    private static double Separation(HorizontalPosition a, HorizontalPosition b) =>
        GuidanceCalculator.AngularDistance(a.AzimuthDegrees, a.AltitudeDegrees, b.AzimuthDegrees, b.AltitudeDegrees);

    // Dawn and dusk of the same day can both qualify for one approach of the Moon: keep the closer, so the user hears about it once.
    private IEnumerable<Conjunction> KeepCloserOfSameApproach(List<Conjunction> found)
    {
        var kept = new List<Conjunction>();
        foreach (var candidate in found.OrderBy(c => c.Best.SeparationDegrees).ThenBy(c => c.Best.Instant))
        {
            var sameApproach = kept.Any(k => k.Companion == candidate.Companion
                && (k.Best.Instant - candidate.Best.Instant).Duration() < Criteria.SameApproachWithin);
            if (!sameApproach)
                kept.Add(candidate);
        }
        return kept;
    }
}
