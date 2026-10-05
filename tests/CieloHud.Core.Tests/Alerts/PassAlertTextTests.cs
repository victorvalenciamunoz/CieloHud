using static CieloHud.Core.Tests.Alerts.AlertPasses;

namespace CieloHud.Core.Tests.Alerts;

public class PassAlertTextTests
{
    private static PassAlert Alert(VisiblePass pass, string notifyAtLocal, bool eveningBefore = false) =>
        new(pass, Local(notifyAtLocal), eveningBefore);

    [Fact]
    public void RisingPass_TheExampleFromThePlan()
    {
        var alert = Alert(Pass(Local("2026-10-14 21:43")), "2026-10-14 21:33");

        Assert.Equal("La ISS pasa en 10 min", PassAlertText.Title(alert));
        Assert.Equal("A las 21:43 pasa la ISS · 5 min · aparece por el NO, máximo 67° al SE", PassAlertText.Body(alert, Madrid));
    }

    [Fact]
    public void FromShadow_AppearsMidSky()
    {
        var alert = Alert(Pass(Local("2026-10-14 21:43"), fromShadow: true), "2026-10-14 21:33");

        Assert.EndsWith("aparece a 20° al SO, máximo 67° al SE", PassAlertText.Body(alert, Madrid));
    }

    [Fact]
    public void LitAfterCulminating_GoesDownFromThere()
    {
        var alert = Alert(Pass(Local("2026-10-14 21:43"), maxAltitude: 37, appearsAtMax: true), "2026-10-14 21:33");

        Assert.EndsWith("aparece a 37° al SE y va bajando", PassAlertText.Body(alert, Madrid));
    }

    [Fact]
    public void EveningBefore_SaysTomorrow()
    {
        var alert = Alert(Pass(Local("2026-10-15 06:25")), "2026-10-14 22:00", eveningBefore: true);

        Assert.Equal("Mañana temprano pasa la ISS", PassAlertText.Title(alert));
        Assert.StartsWith("Mañana a las 6:25 pasa la ISS · ", PassAlertText.Body(alert, Madrid));
    }

    [Fact]
    public void LateAlert_TitleCountsFromWhenItGoesOff()
    {
        var alert = Alert(Pass(Local("2026-10-14 21:43")), "2026-10-14 21:39:10");

        Assert.Equal("La ISS pasa en 4 min", PassAlertText.Title(alert));
    }

    [Theory]
    [InlineData(280, "5 min")]
    [InlineData(20, "1 min")]
    [InlineData(150, "3 min")]
    public void Duration_WholeMinutesAtLeastOne(int seconds, string expected)
    {
        var alert = Alert(Pass(Local("2026-10-14 21:43"), TimeSpan.FromSeconds(seconds)), "2026-10-14 21:33");

        Assert.Contains($" · {expected} · ", PassAlertText.Body(alert, Madrid));
    }

    [Fact]
    public void Altitude_Rounded()
    {
        var alert = Alert(Pass(Local("2026-10-14 21:43"), maxAltitude: 66.6), "2026-10-14 21:33");

        Assert.EndsWith("máximo 67° al SE", PassAlertText.Body(alert, Madrid));
    }

    [Fact]
    public void LocalTime_AfterDaylightSavingEnds()
    {
        // 26 Oct 2026, Madrid already on UTC+1.
        var pass = Pass(DateTimeOffset.Parse("2026-10-26T05:25:00Z"));
        var alert = new PassAlert(pass, DateTimeOffset.Parse("2026-10-25T21:00:00Z"), true);

        Assert.StartsWith("Mañana a las 6:25 ", PassAlertText.Body(alert, Madrid));
    }

    [Theory]
    [InlineData(0, "N")]
    [InlineData(45, "NE")]
    [InlineData(90, "E")]
    [InlineData(135, "SE")]
    [InlineData(180, "S")]
    [InlineData(225, "SO")]
    [InlineData(270, "O")]
    [InlineData(315, "NO")]
    public void CardinalPoints_InSpanish(double azimuth, string expected)
    {
        var start = Local("2026-10-14 21:43");
        var rise = new PassPoint(start, new HorizontalPosition(azimuth, 0));
        var max = new PassPoint(start + TimeSpan.FromMinutes(3), new HorizontalPosition(azimuth, 40));
        var end = new PassPoint(start + TimeSpan.FromMinutes(6), new HorizontalPosition(azimuth, 0));
        var pass = new VisiblePass(new SatellitePass(rise, max, end), rise, max, end);

        var body = PassAlertText.Body(Alert(pass, "2026-10-14 21:33"), Madrid);

        Assert.EndsWith($"aparece por el {expected}, máximo 40° al {expected}", body);
    }
}
