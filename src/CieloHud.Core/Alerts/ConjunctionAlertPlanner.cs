using CieloHud.Core.Conjunctions;

namespace CieloHud.Core.Alerts;

/// <summary>
/// Decides which conjunctions to announce (the Moon next to planets, two planets together); <see cref="AlertPlanner"/> decides
/// when, with the same rules as the ISS passes. Pure: the caller supplies the conjunctions, the current instant, the observer's time zone and those already announced.
/// <list type="bullet">
/// <item>Conjunctions of the Moon whose windows overlap (next to two planets at once) make one alert. Two planets go alone.</item>
/// <item>Each alert goes off <see cref="AlertSettings.ConjunctionLeadTime"/> before its first window opens, and is worth it until the last closes.</item>
/// <item>Conjunctions already announced are left out: the same bodies within <see cref="AlertSettings.SameConjunctionTolerance"/>
/// (the Moon) or <see cref="AlertSettings.SamePlanetPairTolerance"/> (two planets, once per approach).</item>
/// </list>
/// </summary>
public sealed class ConjunctionAlertPlanner
{
    private readonly AlertPlanner _timing;

    public ConjunctionAlertPlanner(AlertSettings? settings = null)
    {
        _timing = new AlertPlanner(settings);
    }

    public AlertSettings Settings => _timing.Settings;

    /// <summary>Alerts for the given conjunctions, ordered by <see cref="ConjunctionAlert.NotifyAt"/>. All instants in UTC.</summary>
    public IReadOnlyList<ConjunctionAlert> Plan(
        IEnumerable<Conjunction> conjunctions,
        DateTimeOffset now,
        TimeZoneInfo timeZone,
        IEnumerable<NotifiedConjunction>? alreadyNotified = null)
    {
        ArgumentNullException.ThrowIfNull(conjunctions);
        var notified = alreadyNotified?.ToList() ?? [];

        var fresh = conjunctions.Where(c => !WasNotified(c, notified)).ToList();
        var groups = Overlapping(fresh.Where(c => c.IsWithMoon))
            .Concat(fresh.Where(c => !c.IsWithMoon).Select(c => new List<Conjunction> { c }));
        var candidates = groups.Select(group =>
        {
            var start = group.Min(c => c.Start.Instant);
            var end = group.Max(c => c.End.Instant);
            return new AlertCandidate<List<Conjunction>>(group, start - Settings.ConjunctionLeadTime, end);
        });

        return _timing.Plan(candidates, now, timeZone)
            .Select(a => new ConjunctionAlert(a.Subject, a.NotifyAt, a.IsEveningBefore))
            .ToList();
    }

    private bool WasNotified(Conjunction conjunction, List<NotifiedConjunction> notified) =>
        notified.Any(n => n.Guide == conjunction.Guide && n.Planet == conjunction.Companion
            && (n.Best - conjunction.Best.Instant).Duration()
                < (conjunction.IsWithMoon ? Settings.SameConjunctionTolerance : Settings.SamePlanetPairTolerance));

    // Windows that overlap in time, chained: each group seen together in the sky. Closest first within a group.
    private static IEnumerable<List<Conjunction>> Overlapping(IEnumerable<Conjunction> conjunctions)
    {
        var group = new List<Conjunction>();
        var groupEnd = DateTimeOffset.MinValue;
        foreach (var conjunction in conjunctions.OrderBy(c => c.Start.Instant))
        {
            if (group.Count > 0 && conjunction.Start.Instant > groupEnd)
            {
                yield return Closest(group);
                group = [];
            }
            group.Add(conjunction);
            groupEnd = group.Count == 1 || conjunction.End.Instant > groupEnd ? conjunction.End.Instant : groupEnd;
        }
        if (group.Count > 0)
            yield return Closest(group);
    }

    private static List<Conjunction> Closest(List<Conjunction> group) =>
        group.OrderBy(c => c.Best.SeparationDegrees).ToList();
}
