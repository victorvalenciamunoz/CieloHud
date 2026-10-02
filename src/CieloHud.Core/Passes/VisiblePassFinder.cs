using CieloHud.Core.Satellites;
using CieloHud.Core.Sky;
using CieloHud.Core.SolarSystem;

namespace CieloHud.Core.Passes;

/// <summary>
/// Combines pass geometry, satellite illumination and Sun altitude. Each geometric pass is sampled every
/// <see cref="VisibilityCriteria.Step"/>; the longest run of samples that are lit and in a dark sky is the visible part,
/// kept only if it peaks above <see cref="VisibilityCriteria.MinPeakAltitudeDegrees"/>.
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

        var visible = samples.Select(s => IsVisibleAt(tle, observer, s)).ToArray();

        // Longest run of visible samples.
        int bestStart = -1, bestLength = 0;
        for (var i = 0; i < samples.Count;)
        {
            if (!visible[i]) { i++; continue; }
            var j = i;
            while (j < samples.Count && visible[j]) j++;
            if (j - i > bestLength) { bestStart = i; bestLength = j - i; }
            i = j;
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

    // Every sample lies within the geometric pass, so being above the horizon is given (the end points sit at ±0.1° of it).
    private bool IsVisibleAt(Tle tle, Observer observer, PassPoint point) =>
        _sun.Locate(observer, point.Instant).AltitudeDegrees <= _criteria.MaxSunAltitudeDegrees
        && _illumination.IsSunlit(tle, point.Instant);
}
