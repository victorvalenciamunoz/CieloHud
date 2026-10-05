using static CieloHud.Core.Tests.Alerts.AlertPasses;

namespace CieloHud.Core.Tests.Alerts;

/// <summary>
/// The timing rules shared by every kind of alert. The ISS-specific cases (quiet-hour edges, the 25 Oct time change…)
/// are in <see cref="PassAlertPlannerTests"/>, which goes through this planner.
/// </summary>
public class AlertPlannerTests
{
    private static readonly DateTimeOffset Morning = Local("2026-10-14 09:00");

    private readonly AlertPlanner _planner = new();

    private static AlertCandidate<string> Candidate(string name, string desired, string worthUntil) =>
        new(name, Local(desired), Local(worthUntil));

    [Fact]
    public void OnTime_GoesOffWhenDesired()
    {
        var alert = Assert.Single(_planner.Plan([Candidate("a", "2026-10-14 20:00", "2026-10-14 23:00")], Morning, Madrid));

        Assert.Equal("a", alert.Subject);
        Assert.Equal(Local("2026-10-14 20:00"), alert.NotifyAt);
        Assert.False(alert.IsEveningBefore);
    }

    [Fact]
    public void QuietHours_EveningBefore()
    {
        var alert = Assert.Single(_planner.Plan([Candidate("a", "2026-10-15 04:20", "2026-10-15 07:45")], Morning, Madrid));

        Assert.Equal(Local("2026-10-14 22:00"), alert.NotifyAt);
        Assert.True(alert.IsEveningBefore);
    }

    [Fact]
    public void Late_StillWorthIt_GoesOffNow()
    {
        var now = Local("2026-10-14 21:00");

        var alert = Assert.Single(_planner.Plan([Candidate("a", "2026-10-14 20:00", "2026-10-14 23:00")], now, Madrid));

        Assert.Equal(now, alert.NotifyAt);
    }

    [Fact]
    public void Late_InQuietHours_Dropped()
    {
        Assert.Empty(_planner.Plan([Candidate("a", "2026-10-15 01:00", "2026-10-15 05:00")], Local("2026-10-15 02:00"), Madrid));
    }

    [Fact]
    public void NoLongerWorthIt_Dropped()
    {
        Assert.Empty(_planner.Plan([Candidate("a", "2026-10-14 08:00", "2026-10-14 09:00")], Morning, Madrid));
    }

    [Fact]
    public void Order_ByNotifyTime_ThenByHowSoonItExpires()
    {
        var alerts = _planner.Plan(
        [
            Candidate("late", "2026-10-14 21:00", "2026-10-14 23:00"),
            Candidate("dawn-long", "2026-10-15 05:00", "2026-10-15 07:45"),
            Candidate("dawn-short", "2026-10-15 06:00", "2026-10-15 06:10"),
        ], Morning, Madrid);

        Assert.Equal(["late", "dawn-short", "dawn-long"], alerts.Select(a => a.Subject));
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _planner.Plan<string>(null!, Morning, Madrid));
        Assert.Throws<ArgumentNullException>(() => _planner.Plan<string>([], Morning, null!));
    }
}
