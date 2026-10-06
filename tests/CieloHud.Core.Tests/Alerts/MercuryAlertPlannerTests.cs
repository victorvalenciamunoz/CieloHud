using static CieloHud.Core.Tests.Alerts.AlertMercury;
using static CieloHud.Core.Tests.Alerts.AlertPasses;

namespace CieloHud.Core.Tests.Alerts;

public class MercuryAlertPlannerTests
{
    private static IReadOnlyList<MercuryAlert> Plan(string now, IEnumerable<MercuryApparition> apparitions,
        IEnumerable<NotifiedMercury>? notified = null) =>
        new MercuryAlertPlanner().Plan(apparitions, Local(now), Madrid, notified);

    [Fact]
    public void Dusk_ThirtyMinutesBeforeTheBestDaysWindow()
    {
        var alert = Assert.Single(Plan("2027-02-04 12:00", [FebruaryDusk()]));

        Assert.Equal(Local("2027-02-04 18:35"), alert.NotifyAt);
        Assert.False(alert.IsEveningBefore);
        Assert.Equal(Local("2027-02-04 19:05"), alert.WindowStart);
        Assert.Equal(Local("2027-02-04 19:11"), alert.WindowEnd);
    }

    [Fact]
    public void Dawn_TheEveningBefore()
    {
        // 6:53 falls in the quiet hours: 22:00 the evening before.
        var alert = Assert.Single(Plan("2026-11-19 12:00", [NovemberDawn()]));

        Assert.Equal(Local("2026-11-19 22:00"), alert.NotifyAt);
        Assert.True(alert.IsEveningBefore);
    }

    [Fact]
    public void PlannedDuringTheWindow_Now()
    {
        var alert = Assert.Single(Plan("2027-02-04 19:08", [FebruaryDusk()]));

        Assert.Equal(Local("2027-02-04 19:08"), alert.NotifyAt);
    }

    [Fact]
    public void WindowClosed_NoAlert()
    {
        Assert.Empty(Plan("2027-02-04 19:12", [FebruaryDusk()]));
    }

    [Fact]
    public void AlreadyAnnounced_NotAgainEvenIfTheBestDayShifts()
    {
        var notified = new NotifiedMercury(TwilightPeriod.Dusk, Local("2027-02-03 19:04"));

        Assert.Empty(Plan("2027-02-04 12:00", [FebruaryDusk()], [notified]));
    }

    [Fact]
    public void AnnouncedSeasonAtTheOtherEndOfTheNight_DoesNotBlock()
    {
        var notified = new NotifiedMercury(TwilightPeriod.Dawn, Local("2027-02-03 07:30"));

        Assert.Single(Plan("2027-02-04 12:00", [FebruaryDusk()], [notified]));
    }

    [Fact]
    public void AnnouncedSeasonMonthsAgo_DoesNotBlock()
    {
        var notified = NotifiedMercury.From(Apparition(TwilightPeriod.Dusk,
            "2026-10-04 19:05", "2026-10-04 19:05", "2026-10-04 19:11", "2026-10-01 19:01", "2026-10-07 19:09"));

        Assert.Single(Plan("2027-02-04 12:00", [FebruaryDusk()], [notified]));
    }

    [Fact]
    public void SeveralSeasons_OrderedByAlertTime()
    {
        var alerts = Plan("2026-11-01 12:00", [FebruaryDusk(), NovemberDawn()]);

        Assert.Equal([Local("2026-11-19 22:00"), Local("2027-02-04 18:35")], alerts.Select(a => a.NotifyAt));
    }

    [Fact]
    public void NotifiedMercury_FromTheBestMoment()
    {
        Assert.Equal(new NotifiedMercury(TwilightPeriod.Dawn, Local("2026-11-20 07:37")), NotifiedMercury.From(NovemberDawn()));
    }
}
