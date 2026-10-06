namespace CieloHud.Core.Tests.Cards;

public class MoonPhasesTests
{
    private static readonly DateTimeOffset FirstQuarterAt = new(2026, 10, 18, 16, 13, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FullMoonAt = new(2026, 10, 26, 4, 12, 0, TimeSpan.Zero);
    private static readonly MoonQuarter FirstQuarter = new(MoonQuarterKind.FirstQuarter, FirstQuarterAt);
    private static readonly MoonQuarter FullMoon = new(MoonQuarterKind.FullMoon, FullMoonAt);

    [Fact]
    public void Name_OnTheInstant_IsThePrincipalPhase()
    {
        Assert.Equal(MoonPhaseName.FirstQuarter, MoonPhases.Name(90, FirstQuarter, FullMoon, FirstQuarterAt));
    }

    [Theory]
    [InlineData(-24)] // a day before the next one
    [InlineData(-1)]
    public void Name_UpToADayBefore_IsTheComingPrincipalPhase(double hoursFromFull)
    {
        var instant = FullMoonAt.AddHours(hoursFromFull);

        Assert.Equal(MoonPhaseName.FullMoon, MoonPhases.Name(179, FirstQuarter, FullMoon, instant));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(24)]
    public void Name_UpToADayAfter_IsThePastPrincipalPhase(double hoursAfterFirstQuarter)
    {
        var instant = FirstQuarterAt.AddHours(hoursAfterFirstQuarter);

        Assert.Equal(MoonPhaseName.FirstQuarter, MoonPhases.Name(100, FirstQuarter, FullMoon, instant));
    }

    [Fact]
    public void Name_JustOverADayFromBoth_IsTheStretchBetween()
    {
        var afterFirstQuarter = FirstQuarterAt.AddHours(24).AddMinutes(1);
        var beforeFull = FullMoonAt.AddHours(-24).AddMinutes(-1);

        Assert.Equal(MoonPhaseName.WaxingGibbous, MoonPhases.Name(102, FirstQuarter, FullMoon, afterFirstQuarter));
        Assert.Equal(MoonPhaseName.WaxingGibbous, MoonPhases.Name(167, FirstQuarter, FullMoon, beforeFull));
    }

    [Theory]
    [InlineData(45, MoonPhaseName.WaxingCrescent)]
    [InlineData(135, MoonPhaseName.WaxingGibbous)]
    [InlineData(225, MoonPhaseName.WaningGibbous)]
    [InlineData(315, MoonPhaseName.WaningCrescent)]
    public void Name_BetweenPrincipalPhases_FollowsThePhaseAngle(double phaseDegrees, MoonPhaseName expected)
    {
        // Quarters far from the instant: only the angle decides.
        var instant = new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
        var previous = new MoonQuarter(MoonQuarterKind.NewMoon, instant.AddDays(-3));
        var next = new MoonQuarter(MoonQuarterKind.FirstQuarter, instant.AddDays(3));

        Assert.Equal(expected, MoonPhases.Name(phaseDegrees, previous, next, instant));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(360)]
    [InlineData(double.NaN)]
    public void Name_PhaseOutOfRange_Throws(double phaseDegrees)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MoonPhases.Name(phaseDegrees, FirstQuarter, FullMoon, FirstQuarterAt.AddDays(3)));
    }
}
