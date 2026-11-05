namespace CieloHud.Core.Tests.Alerts;

public class AlarmStepsTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-05T10:00:00Z");

    /// <summary>The model measured on ColorOS: window of 75 % of the delay, at most one hour.</summary>
    private static TimeSpan Window(TimeSpan delay)
    {
        var window = delay * AlarmSteps.WindowFraction;
        return window < AlarmSteps.MaxWindow ? window : AlarmSteps.MaxWindow;
    }

    [Fact]
    public void FarTarget_OneHourBefore()
    {
        var target = Now + TimeSpan.FromDays(2);

        Assert.Equal(target - TimeSpan.FromHours(1), AlarmSteps.NextWakeUp(Now, target));
    }

    [Fact]
    public void NearTarget_WindowEndsOnTarget()
    {
        // 30 s ahead: asks at +17.1 s; 75 % of that is 12.9 s, ending at +30 s. The OPPO test alarm went off at the end of its window.
        var target = Now + TimeSpan.FromSeconds(30);

        var wake = AlarmSteps.NextWakeUp(Now, target);

        Assert.Equal(TimeSpan.FromSeconds(30 / 1.75).TotalMilliseconds, (wake - Now).TotalMilliseconds, 1);
    }

    [Fact]
    public void PastTarget_Now()
    {
        Assert.Equal(Now, AlarmSteps.NextWakeUp(Now, Now - TimeSpan.FromMinutes(1)));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(600)]
    [InlineData(4800)]   // 80 min: where the window reaches its cap
    [InlineData(8399)]
    [InlineData(8400)]   // 140 min: where the two formulas meet
    [InlineData(8401)]
    [InlineData(259200)] // three days
    public void EndOfWindow_NeverPastTarget_AndNotMuchBefore(int seconds)
    {
        var target = Now + TimeSpan.FromSeconds(seconds);

        var wake = AlarmSteps.NextWakeUp(Now, target);
        var latest = wake + Window(wake - Now);

        Assert.True(wake > Now);
        Assert.InRange((target - latest).TotalMilliseconds, -1, 1);
    }

    [Theory]
    [InlineData("exact")]
    [InlineData("end of window")]
    [InlineData("middle of window")]
    public void ThreeDaysAway_ReachesTheTargetInAFewWakeUps(string delivery)
    {
        var target = Now + TimeSpan.FromDays(3);
        var now = Now;
        var wakeUps = 0;

        while (!AlarmSteps.IsDue(now, target))
        {
            var wake = AlarmSteps.NextWakeUp(now, target);
            var window = Window(wake - now);
            now = delivery switch
            {
                "exact" => wake,
                "end of window" => wake + window,
                _ => wake + window / 2,
            };
            wakeUps++;
            Assert.True(now <= target, $"went off after the target at wake-up {wakeUps}");
        }

        Assert.InRange(wakeUps, 1, 10);
        Assert.True(target - now <= AlarmSteps.DueMargin);
    }

    [Theory]
    [InlineData(31, false)]
    [InlineData(30, true)]
    [InlineData(-60, true)]
    public void IsDue_WithinMargin(int secondsToTarget, bool due)
    {
        Assert.Equal(due, AlarmSteps.IsDue(Now, Now + TimeSpan.FromSeconds(secondsToTarget)));
    }
}
