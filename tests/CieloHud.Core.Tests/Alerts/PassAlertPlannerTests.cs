using static CieloHud.Core.Tests.Alerts.AlertPasses;

namespace CieloHud.Core.Tests.Alerts;

public class PassAlertPlannerTests
{
    private static readonly DateTimeOffset FreshEpoch = Local("2026-10-13 12:00");
    private static readonly DateTimeOffset Morning = Local("2026-10-14 09:00");

    private readonly PassAlertPlanner _planner = new();

    private IReadOnlyList<PassAlert> Plan(DateTimeOffset now, params VisiblePass[] passes) =>
        _planner.Plan(passes, FreshEpoch, now, Madrid);

    [Fact]
    public void EveningPass_TenMinutesBefore()
    {
        var alert = Assert.Single(Plan(Morning, Pass(Local("2026-10-14 21:43"))));

        Assert.Equal(Local("2026-10-14 21:33"), alert.NotifyAt);
        Assert.False(alert.IsEveningBefore);
    }

    [Fact]
    public void DawnPass_EveningBeforeAtTen()
    {
        var alert = Assert.Single(Plan(Morning, Pass(Local("2026-10-15 06:25"))));

        Assert.Equal(Local("2026-10-14 22:00"), alert.NotifyAt);
        Assert.True(alert.IsEveningBefore);
    }

    [Theory]
    [InlineData("2026-10-15 07:09:59", true)]  // alert at 6:59:59, still quiet
    [InlineData("2026-10-15 07:10:00", false)] // alert at 7:00, quiet hours over
    [InlineData("2026-10-15 00:09:59", false)] // alert at 23:59:59 the day before
    [InlineData("2026-10-15 00:10:00", true)]  // alert at 0:00, quiet hours start
    public void QuietHours_JudgedByAlertTime(string visibleStart, bool eveningBefore)
    {
        var alert = Assert.Single(Plan(Morning, Pass(Local(visibleStart))));

        Assert.Equal(eveningBefore, alert.IsEveningBefore);
        var expected = eveningBefore ? Local("2026-10-14 22:00") : Local(visibleStart) - TimeSpan.FromMinutes(10);
        Assert.Equal(expected, alert.NotifyAt);
    }

    [Fact]
    public void Late_GoesOffNow()
    {
        var now = Local("2026-10-14 21:38");

        var alert = Assert.Single(Plan(now, Pass(Local("2026-10-14 21:43"))));

        Assert.Equal(now, alert.NotifyAt);
    }

    [Fact]
    public void LateInQuietHours_Dropped()
    {
        Assert.Empty(Plan(Local("2026-10-15 05:00"), Pass(Local("2026-10-15 06:25"))));
    }

    [Fact]
    public void EveningReminderMissed_GoesOffNowBeforeMidnight()
    {
        var now = Local("2026-10-14 23:30");

        var alert = Assert.Single(Plan(now, Pass(Local("2026-10-15 06:25"))));

        Assert.Equal(now, alert.NotifyAt);
        Assert.True(alert.IsEveningBefore);
    }

    [Fact]
    public void StartedPasses_Dropped()
    {
        var start = Local("2026-10-14 21:43");

        Assert.Empty(Plan(start, Pass(start)));
        Assert.Empty(Plan(start + TimeSpan.FromMinutes(1), Pass(start)));
    }

    [Fact]
    public void OldTle_LaterPassesDropped()
    {
        var epoch = Local("2026-10-10 21:00");
        var lastTrusted = Pass(Local("2026-10-14 21:00"));
        var tooLate = Pass(Local("2026-10-14 21:00:01"));

        var alerts = _planner.Plan([lastTrusted, tooLate], epoch, Local("2026-10-14 12:00"), Madrid);

        Assert.Same(lastTrusted, Assert.Single(alerts).Pass);
    }

    [Fact]
    public void AlreadyNotified_MatchedWithTolerance()
    {
        var start = Local("2026-10-14 21:43");
        var passes = new[] { Pass(start) };

        // A newer TLE moves the same pass by seconds; a pass three minutes away is another one.
        Assert.Empty(_planner.Plan(passes, FreshEpoch, Morning, Madrid, [start + TimeSpan.FromSeconds(90)]));
        Assert.Single(_planner.Plan(passes, FreshEpoch, Morning, Madrid, [start + TimeSpan.FromMinutes(3)]));
    }

    [Fact]
    public void OrderedByAlertTime_TwoDawnPassesBothInTheEvening()
    {
        var late = Pass(Local("2026-10-15 21:00"));
        var dawnSecond = Pass(Local("2026-10-15 06:40"));
        var dawnFirst = Pass(Local("2026-10-15 05:05"));

        var alerts = Plan(Morning, late, dawnSecond, dawnFirst);

        Assert.Equal([dawnFirst, dawnSecond, late], alerts.Select(a => a.Pass));
        Assert.All(alerts.Take(2), a => Assert.Equal(Local("2026-10-14 22:00"), a.NotifyAt));
    }

    [Fact]
    public void DaylightSavingEnd_EveningInEachDaysOwnOffset()
    {
        // Madrid goes from UTC+2 to UTC+1 at 3:00 on 25 Oct 2026.
        var epoch = Local("2026-10-24 06:00");
        var saturdayEvening = Assert.Single(_planner.Plan([Pass(Local("2026-10-25 06:25"))], epoch, Local("2026-10-24 12:00"), Madrid)).NotifyAt;
        var sundayEvening = Assert.Single(_planner.Plan([Pass(Local("2026-10-26 06:25"))], epoch, Local("2026-10-25 12:00"), Madrid)).NotifyAt;

        Assert.Equal(DateTimeOffset.Parse("2026-10-24T20:00:00Z"), saturdayEvening);
        Assert.Equal(DateTimeOffset.Parse("2026-10-25T21:00:00Z"), sundayEvening);
    }

    [Fact]
    public void Instants_InUtc()
    {
        var alert = Assert.Single(Plan(Local("2026-10-14 21:38"), Pass(Local("2026-10-14 21:43"))));

        Assert.Equal(TimeSpan.Zero, alert.NotifyAt.Offset);
    }

    [Fact]
    public void LeadTime_Configurable()
    {
        var planner = new PassAlertPlanner(new AlertSettings { LeadTime = TimeSpan.FromMinutes(30) });

        var alert = Assert.Single(planner.Plan([Pass(Local("2026-10-14 21:43"))], FreshEpoch, Morning, Madrid));

        Assert.Equal(Local("2026-10-14 21:13"), alert.NotifyAt);
    }

    [Theory]
    [InlineData(-1, 0, 7, 22)]   // negative lead time
    [InlineData(10, 7, 7, 22)]   // empty quiet hours
    [InlineData(10, 0, 23, 22)]  // reminder inside the quiet hours
    [InlineData(10, 0, 7, 24)]   // reminder past midnight
    public void InvalidSettings_Throw(int leadMinutes, int quietStart, int quietEnd, int evening)
    {
        var settings = new AlertSettings
        {
            LeadTime = TimeSpan.FromMinutes(leadMinutes),
            QuietStart = TimeSpan.FromHours(quietStart),
            QuietEnd = TimeSpan.FromHours(quietEnd),
            EveningReminder = TimeSpan.FromHours(evening),
        };

        Assert.ThrowsAny<ArgumentException>(() => new PassAlertPlanner(settings));
    }

    [Theory]
    [InlineData("2026-10-14 23:59:59", false)]
    [InlineData("2026-10-15 00:00", true)]
    [InlineData("2026-10-15 06:59:59", true)]
    [InlineData("2026-10-15 07:00", false)]
    public void IsQuiet_LocalTimeOfDay(string localTime, bool quiet)
    {
        Assert.Equal(quiet, _planner.IsQuiet(Local(localTime), Madrid));
    }
}
