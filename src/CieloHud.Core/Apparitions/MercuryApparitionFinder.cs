using CieloHud.Core.Sky;
using CieloHud.Core.SolarSystem;

namespace CieloHud.Core.Apparitions;

/// <summary>
/// Finds Mercury's seasons by sampling the sky (decision 033). Its greatest elongation is not enough: what matters is how high it
/// is once the sky is dark, and that depends on the angle of the ecliptic with the horizon (in autumn evenings it lies flat and
/// Mercury stays a few degrees up even 25° from the Sun).
/// <para>
/// A window is a run of samples with the Sun below <see cref="MercuryCriteria.MaxSunAltitudeDegrees"/> and Mercury above
/// <see cref="MercuryCriteria.MinAltitudeDegrees"/>, kept if Mercury is bright enough at its best moment, the highest sample.
/// A first pass every <see cref="MercuryCriteria.CoarseStep"/> finds where the sky is near those limits; only there is it sampled
/// every <see cref="MercuryCriteria.Step"/>. Days in a row with a window, at dusk or at dawn, are a season; its best day is the one
/// with Mercury highest.
/// </para>
/// </summary>
public sealed class MercuryApparitionFinder : IMercuryApparitionFinder
{
    private readonly ISolarSystemService _solarSystem;
    private readonly ISunService _sun;
    private readonly IMagnitudeService _magnitude;

    public MercuryApparitionFinder(ISolarSystemService solarSystem, ISunService sun, IMagnitudeService magnitude, MercuryCriteria? criteria = null)
    {
        _solarSystem = solarSystem;
        _sun = sun;
        _magnitude = magnitude;
        Criteria = criteria ?? MercuryCriteria.Default;
        Criteria.Validate();
    }

    public MercuryCriteria Criteria { get; }

    public IReadOnlyList<MercuryApparition> Find(Observer observer, DateTimeOffset from, DateTimeOffset to, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        // The whole season is needed to know which day is the best.
        var windows = FindWindows(observer, from - Criteria.ApparitionSearch, to + Criteria.ApparitionSearch, timeZone);

        var result = new List<MercuryApparition>();
        foreach (var season in Seasons(windows, timeZone))
        {
            var best = season.MaxBy(w => w.Best.Mercury.AltitudeDegrees)!;
            if (best.End.Instant >= from && best.Start.Instant < to)
                result.Add(new MercuryApparition(best.Period, best, season[0], season[^1], season.Count));
        }
        return result.OrderBy(a => a.Best.Best.Instant).ToList();
    }

    public IReadOnlyList<MercuryWindow> FindWindows(Observer observer, DateTimeOffset from, DateTimeOffset to, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var windows = new List<MercuryWindow>();
        foreach (var (first, last) in NearLimits(observer, from.ToUniversalTime(), to))
        {
            var run = new List<MercuryPoint>();
            for (var t = first - Criteria.CoarseStep; t <= last + Criteria.CoarseStep; t += Criteria.Step)
            {
                var point = At(observer, t);
                if (Meets(point))
                {
                    run.Add(point);
                    continue;
                }
                AddWindow(run);
                run.Clear();
            }
            AddWindow(run);
        }
        return windows;

        void AddWindow(List<MercuryPoint> run)
        {
            if (run.Count == 0 || run[0].Instant < from || run[0].Instant >= to)
                return;
            var best = run.MaxBy(p => p.Mercury.AltitudeDegrees);
            var magnitude = _magnitude.Magnitude(CelestialBody.Mercury, best.Instant);
            if (magnitude > Criteria.MaxMagnitude)
                return;
            // Twilight after local noon is dusk; before it, dawn.
            var period = TimeZoneInfo.ConvertTime(best.Instant, timeZone).Hour >= 12 ? TwilightPeriod.Dusk : TwilightPeriod.Dawn;
            windows.Add(new MercuryWindow(period, run[0], best, run[^1], magnitude));
        }
    }

    /// <summary>
    /// Runs of coarse samples with the Sun and Mercury within <see cref="MercuryCriteria.CoarseMarginDegrees"/> of the limits, from
    /// <paramref name="from"/> to <paramref name="to"/> and past it while a run is still open.
    /// </summary>
    private List<(DateTimeOffset First, DateTimeOffset Last)> NearLimits(Observer observer, DateTimeOffset from, DateTimeOffset to)
    {
        var runs = new List<(DateTimeOffset, DateTimeOffset)>();
        DateTimeOffset? first = null, last = null;
        for (var t = TimeGrid.AlignUp(from, Criteria.CoarseStep); t < to || last == t - Criteria.CoarseStep; t += Criteria.CoarseStep)
        {
            var near = _sun.Locate(observer, t).AltitudeDegrees <= Criteria.MaxSunAltitudeDegrees + Criteria.CoarseMarginDegrees
                && _solarSystem.Locate(CelestialBody.Mercury, observer, t).AltitudeDegrees >= Criteria.MinAltitudeDegrees - Criteria.CoarseMarginDegrees;
            if (near)
            {
                first ??= t;
                last = t;
            }
            else if (first is { } f && last is { } l)
            {
                runs.Add((f, l));
                first = last = null;
            }
        }
        if (first is { } openFirst && last is { } openLast)
            runs.Add((openFirst, openLast));
        return runs;
    }

    private MercuryPoint At(Observer observer, DateTimeOffset t) =>
        new(t, _solarSystem.Locate(CelestialBody.Mercury, observer, t), _sun.Locate(observer, t).AltitudeDegrees);

    private bool Meets(MercuryPoint point) =>
        point.SunAltitudeDegrees <= Criteria.MaxSunAltitudeDegrees && point.Mercury.AltitudeDegrees >= Criteria.MinAltitudeDegrees;

    /// <summary>Windows on consecutive local days, at the same end of the night.</summary>
    private static IEnumerable<List<MercuryWindow>> Seasons(IEnumerable<MercuryWindow> windows, TimeZoneInfo timeZone)
    {
        foreach (var period in windows.GroupBy(w => w.Period))
        {
            var season = new List<MercuryWindow>();
            DateOnly? previous = null;
            foreach (var window in period.OrderBy(w => w.Best.Instant))
            {
                var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(window.Best.Instant, timeZone).DateTime);
                if (previous is { } p && day.DayNumber - p.DayNumber > 1)
                {
                    yield return season;
                    season = [];
                }
                season.Add(window);
                previous = day;
            }
            if (season.Count > 0)
                yield return season;
        }
    }
}
