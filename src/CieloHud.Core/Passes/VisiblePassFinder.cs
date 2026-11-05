using CieloHud.Core.Satellites;
using CieloHud.Core.Sky;
using CieloHud.Core.SolarSystem;

namespace CieloHud.Core.Passes;

/// <summary>
/// Combines pass geometry, satellite illumination and Sun altitude. Each geometric pass is sampled every
/// <see cref="VisibilityCriteria.Step"/>. The visible part starts at the first sample where the satellite is lit and the
/// sky is dark, and lasts while the satellite stays lit: once you are watching it, a slowly brightening dawn sky does not
/// make it vanish (a pass lasts minutes; the Sun climbs under 2° in that time). This matches how Heavens-Above lists
/// dawn passes that start in darkness and end in civil twilight. Kept only if it peaks above
/// <see cref="VisibilityCriteria.MinPeakAltitudeDegrees"/>.
/// </summary>
public sealed class VisiblePassFinder : IVisiblePassFinder
{
    private readonly ISatellitePassPredictor _passes;
    private readonly ISatelliteService _satellite;
    private readonly ISunService _sun;
    private readonly ISatelliteIlluminationService _illumination;
    private readonly VisibilityCriteria _criteria;

    public VisiblePassFinder(
        ISatellitePassPredictor passes,
        ISatelliteService satellite,
        ISunService sun,
        ISatelliteIlluminationService illumination,
        VisibilityCriteria? criteria = null)
    {
        _passes = passes;
        _satellite = satellite;
        _sun = sun;
        _illumination = illumination;
        _criteria = criteria ?? VisibilityCriteria.Default;
    }

    public IReadOnlyList<VisiblePass> Find(Tle tle, Observer observer, DateTimeOffset from, DateTimeOffset to)
    {
        var result = new List<VisiblePass>();
        foreach (var pass in _passes.Predict(tle, observer, from, to))
        {
            if (Analyze(tle, observer, pass) is { } visible)
                result.Add(visible);
        }
        return result;
    }

    private VisiblePass? Analyze(Tle tle, Observer observer, SatellitePass pass)
    {
        // Sample the pass, always including its exact end.
        var samples = new List<PassPoint>();
        for (var t = pass.Start.Instant; t < pass.End.Instant; t += _criteria.Step)
            samples.Add(t == pass.Start.Instant ? pass.Start : new PassPoint(t, _satellite.Locate(tle, observer, t)));
        samples.Add(pass.End);

        var lit = samples.Select(s => _illumination.IsSunlit(tle, s.Instant)).ToArray();
        var dark = samples.Select(s => _sun.Locate(observer, s.Instant).AltitudeDegrees <= _criteria.MaxSunAltitudeDegrees).ToArray();

        // For each run of lit samples, the visible part goes from its first dark sample to the end of the run. Keep the longest.
        int bestStart = -1, bestLength = 0;
        for (var i = 0; i < samples.Count;)
        {
            if (!lit[i]) { i++; continue; }
            var runEnd = i;
            while (runEnd < samples.Count && lit[runEnd]) runEnd++;

            var firstDark = i;
            while (firstDark < runEnd && !dark[firstDark]) firstDark++;

            if (runEnd - firstDark > bestLength) { bestStart = firstDark; bestLength = runEnd - firstDark; }
            i = runEnd;
        }
        if (bestLength == 0)
            return null;

        var run = samples.GetRange(bestStart, bestLength);
        var start = run[0];
        var end = run[^1];

        // Use the exact geometric maximum when it falls inside the visible run; otherwise the highest sample.
        var max = pass.Max.Instant >= start.Instant && pass.Max.Instant <= end.Instant
            ? pass.Max
            : run.MaxBy(p => p.Position.AltitudeDegrees);

        if (max.Position.AltitudeDegrees < _criteria.MinPeakAltitudeDegrees)
            return null;

        return new VisiblePass(pass, start, max, end);
    }
}
