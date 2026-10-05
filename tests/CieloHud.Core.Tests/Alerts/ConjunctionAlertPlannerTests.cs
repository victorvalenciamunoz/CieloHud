using static CieloHud.Core.Tests.Alerts.AlertConjunctions;
using static CieloHud.Core.Tests.Alerts.AlertPasses;

namespace CieloHud.Core.Tests.Alerts;

public class ConjunctionAlertPlannerTests
{
    private static readonly DateTimeOffset Morning = Local("2026-10-14 09:00");

    private readonly ConjunctionAlertPlanner _planner = new();

    private IReadOnlyList<ConjunctionAlert> Plan(DateTimeOffset now, params MoonPlanetConjunction[] conjunctions) =>
        _planner.Plan(conjunctions, now, Madrid);

    [Fact]
    public void EveningWindow_HalfAnHourBeforeItOpens()
    {
        var jupiter = Conjunction(CelestialBody.Jupiter, "2026-10-14 20:15", "2026-10-14 22:00", "2026-10-15 01:00");

        var alert = Assert.Single(Plan(Morning, jupiter));

        Assert.Equal(Local("2026-10-14 19:45"), alert.NotifyAt);
        Assert.False(alert.IsEveningBefore);
        Assert.Same(jupiter, alert.Closest);
        Assert.Equal(jupiter.Start.Instant, alert.WindowStart);
        Assert.Equal(jupiter.End.Instant, alert.WindowEnd);
    }

    [Fact]
    public void DawnWindow_EveningBeforeAtTen()
    {
        var jupiter = Conjunction(CelestialBody.Jupiter, "2026-10-15 04:50", "2026-10-15 07:45", "2026-10-15 07:45");

        var alert = Assert.Single(Plan(Morning, jupiter));

        Assert.Equal(Local("2026-10-14 22:00"), alert.NotifyAt);
        Assert.True(alert.IsEveningBefore);
    }

    [Fact]
    public void WindowOpenedAlready_GoesOffNow()
    {
        var now = Local("2026-10-14 21:00");
        var jupiter = Conjunction(CelestialBody.Jupiter, "2026-10-14 20:15", "2026-10-14 22:00", "2026-10-15 01:00");

        Assert.Equal(now, Assert.Single(Plan(now, jupiter)).NotifyAt);
    }

    [Fact]
    public void WindowClosed_Dropped()
    {
        var jupiter = Conjunction(CelestialBody.Jupiter, "2026-10-14 04:50", "2026-10-14 07:45", "2026-10-14 07:45");

        Assert.Empty(Plan(Morning, jupiter));
    }

    [Fact]
    public void AlreadyNotified_SamePlanetWithinADay_Dropped()
    {
        // The dawn of this approach was announced; the dusk the same day is the same approach.
        var dusk = Conjunction(CelestialBody.Jupiter, "2026-10-14 20:15", "2026-10-14 20:15", "2026-10-14 23:00");
        var dawn = new NotifiedConjunction(CelestialBody.Jupiter, Local("2026-10-14 07:40"));

        Assert.Empty(_planner.Plan([dusk], Morning, Madrid, [dawn]));
    }

    [Fact]
    public void AlreadyNotified_OtherPlanetOrAnotherMonth_StillAnnounced()
    {
        var jupiter = Conjunction(CelestialBody.Jupiter, "2026-10-14 20:15", "2026-10-14 22:00", "2026-10-15 01:00");
        NotifiedConjunction[] notified =
        [
            new(CelestialBody.Mars, Local("2026-10-14 22:00")),
            new(CelestialBody.Jupiter, Local("2026-09-16 22:00")),
        ];

        Assert.Single(_planner.Plan([jupiter], Morning, Madrid, notified));
    }

    [Fact]
    public void NotifiedFrom_KeepsPlanetAndBestMoment()
    {
        var jupiter = Conjunction(CelestialBody.Jupiter, "2026-10-14 20:15", "2026-10-14 22:00", "2026-10-15 01:00");

        Assert.Equal(new NotifiedConjunction(CelestialBody.Jupiter, Local("2026-10-14 22:00")), NotifiedConjunction.From(jupiter));
    }

    [Fact]
    public void OverlappingWindows_OneAlert_ClosestFirst()
    {
        // Like 15 Apr 2027: Jupiter and Mars next to the Moon at the same time.
        var jupiter = Conjunction(CelestialBody.Jupiter, "2026-10-14 21:25", "2026-10-14 21:25", "2026-10-15 00:45", separation: 3.5);
        var mars = Conjunction(CelestialBody.Mars, "2026-10-14 21:25", "2026-10-14 23:35", "2026-10-15 04:10", separation: 3.4);

        var alert = Assert.Single(Plan(Morning, jupiter, mars));

        Assert.Equal([CelestialBody.Mars, CelestialBody.Jupiter], alert.Conjunctions.Select(c => c.Planet));
        Assert.Equal(Local("2026-10-14 20:55"), alert.NotifyAt);
        Assert.Equal(Local("2026-10-15 04:10"), alert.WindowEnd);
    }

    [Fact]
    public void ChainedOverlaps_OneAlert()
    {
        // Venus overlaps Jupiter only, Jupiter overlaps Saturn: the three are one evening.
        var venus = Conjunction(CelestialBody.Venus, "2026-10-14 20:00", "2026-10-14 20:30", "2026-10-14 21:00");
        var jupiter = Conjunction(CelestialBody.Jupiter, "2026-10-14 20:45", "2026-10-14 22:00", "2026-10-14 23:00");
        var saturn = Conjunction(CelestialBody.Saturn, "2026-10-14 22:30", "2026-10-14 23:00", "2026-10-14 23:30");

        var alert = Assert.Single(Plan(Morning, saturn, venus, jupiter));

        Assert.Equal(3, alert.Conjunctions.Count);
    }

    [Fact]
    public void SeparateWindows_SeparateAlerts()
    {
        // The same night, Jupiter in the evening and Saturn before dawn: different moments, different alerts.
        var jupiter = Conjunction(CelestialBody.Jupiter, "2026-10-14 20:15", "2026-10-14 21:00", "2026-10-14 23:00");
        var saturn = Conjunction(CelestialBody.Saturn, "2026-10-15 05:00", "2026-10-15 06:00", "2026-10-15 07:30");

        var alerts = Plan(Morning, saturn, jupiter);

        Assert.Equal([CelestialBody.Jupiter, CelestialBody.Saturn], alerts.Select(a => a.Closest.Planet));
        Assert.Equal([Local("2026-10-14 19:45"), Local("2026-10-14 22:00")], alerts.Select(a => a.NotifyAt));
    }

    [Fact]
    public void LeadTime_Configurable()
    {
        var planner = new ConjunctionAlertPlanner(new AlertSettings { ConjunctionLeadTime = TimeSpan.FromHours(1) });
        var jupiter = Conjunction(CelestialBody.Jupiter, "2026-10-14 20:15", "2026-10-14 22:00", "2026-10-15 01:00");

        Assert.Equal(Local("2026-10-14 19:15"), Assert.Single(planner.Plan([jupiter], Morning, Madrid)).NotifyAt);
    }

    [Fact]
    public void NegativeLeadTime_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ConjunctionAlertPlanner(new AlertSettings { ConjunctionLeadTime = TimeSpan.FromMinutes(-1) }));
    }

    [Fact]
    public void Nothing_NoAlerts()
    {
        Assert.Empty(Plan(Morning));
    }
}
