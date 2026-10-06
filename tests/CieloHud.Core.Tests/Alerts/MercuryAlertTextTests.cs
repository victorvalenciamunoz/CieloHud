using static CieloHud.Core.Tests.Alerts.AlertMercury;
using static CieloHud.Core.Tests.Alerts.AlertPasses;

namespace CieloHud.Core.Tests.Alerts;

public class MercuryAlertTextTests
{
    private static MercuryAlert Alert(string notifyAt, MercuryApparition apparition, bool eveningBefore = false) =>
        new(apparition, Local(notifyAt), eveningBefore);

    [Fact]
    public void Dawn_TheEveningBefore()
    {
        var alert = Alert("2026-11-19 22:00", NovemberDawn(), eveningBefore: true);

        Assert.Equal("Mercurio al amanecer", MercuryAlertText.Title(alert));
        Assert.Equal("Mañana al amanecer, Mercurio a 12°, lo más alto en estas semanas · mejor hacia las 7:35 al SE · se ve del 14 al 28 nov",
            MercuryAlertText.Body(alert, Madrid));
    }

    [Fact]
    public void Dusk_TheSameDay()
    {
        var alert = Alert("2027-02-04 18:35", FebruaryDusk());

        Assert.Equal("Mercurio al anochecer", MercuryAlertText.Title(alert));
        Assert.Equal("Hoy al anochecer, Mercurio a 11°, lo más alto en estas semanas · mejor hacia las 19:05 al SO · se ve del 31 ene al 7 feb",
            MercuryAlertText.Body(alert, Madrid));
    }

    [Fact]
    public void TwoDaysAhead_TheDate()
    {
        var alert = Alert("2027-02-02 18:35", FebruaryDusk());

        Assert.StartsWith("El 4 feb al anochecer, Mercurio a 11°", MercuryAlertText.Body(alert, Madrid));
    }

    [Fact]
    public void LateBeforeTheBestMoment_Now()
    {
        // At dawn the best moment is the end of the window.
        var alert = Alert("2026-11-20 07:30", NovemberDawn());

        Assert.StartsWith("Ahora, Mercurio a 12°, lo más alto en estas semanas · mejor hacia las 7:35 al SE", MercuryAlertText.Body(alert, Madrid));
    }

    [Fact]
    public void LatePastTheBestMoment_WhereAndUntilWhen()
    {
        var alert = Alert("2027-02-04 19:08", FebruaryDusk());

        Assert.Equal("Ahora, Mercurio bajo al O, hasta las 19:11 · se ve del 31 ene al 7 feb", MercuryAlertText.Body(alert, Madrid));
    }

    [Fact]
    public void SingleDay_NoRange()
    {
        var oneDay = Apparition(TwilightPeriod.Dusk, "2027-02-04 19:05", "2027-02-04 19:05", "2027-02-04 19:11",
            "2027-02-04 19:05", "2027-02-04 19:05", days: 1);

        Assert.EndsWith("mejor hacia las 19:05 al SE", MercuryAlertText.Body(Alert("2027-02-04 18:35", oneDay), Madrid));
    }

    // Window start, best moment, window end, and the time said.
    [Theory]
    [InlineData("22:06", "22:06", "22:25", "22:10")]   // dusk: the best moment is the start; 22:05 would be before the dark
    [InlineData("7:23", "7:37", "7:37", "7:35")]       // dawn: the best moment is the end; 7:40 would be after it
    [InlineData("19:05", "19:05", "19:11", "19:05")]   // already on the mark
    [InlineData("18:58", "19:02", "19:20", "19:00")]   // the nearest mark
    [InlineData("19:01", "19:02", "19:20", "19:05")]   // the nearest one is before the window: the next
    [InlineData("18:47", "18:47", "18:49", "18:47")]   // no mark inside: the exact minute
    public void Approximate_NearestFiveMinutesWithinTheWindow(string start, string best, string end, string said)
    {
        var apparition = Apparition(TwilightPeriod.Dusk, $"2027-05-26 {start}", $"2027-05-26 {best}", $"2027-05-26 {end}",
            "2027-05-15 21:56", "2027-05-28 22:08");

        var approximate = MercuryAlertText.Approximate(apparition.Best, Madrid);

        Assert.Equal(Local($"2027-05-26 {said}"), approximate);
    }

    [Fact]
    public void Title_ByPeriod()
    {
        Assert.Equal("Mercurio al anochecer", MercuryAlertText.Title(TwilightPeriod.Dusk));
        Assert.Equal("Mercurio al amanecer", MercuryAlertText.Title(TwilightPeriod.Dawn));
    }
}
