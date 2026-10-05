using CieloHud.Core.Tests.Satellites;
using static CieloHud.Core.Tests.Alerts.AlertPasses;

namespace CieloHud.Core.Tests.Alerts;

/// <summary>
/// Real passes from the fixed ISS TLE over Humanes de Madrid, the ones validated against Heavens-Above in phase 2
/// (docs/STATUS.md, phase 2 step 6), turned into alerts.
/// </summary>
public class PassAlertIntegrationTests
{
    private static readonly Observer Humanes = new(40.2509, -3.8271, 0);

    // The fixed TLE is from 1 Oct; these passes are two weeks later, so the TLE age limit is lifted.
    private readonly PassAlertPlanner _planner = new(new AlertSettings { MaxTleAge = TimeSpan.FromDays(30) });

    private IReadOnlyList<PassAlert> AlertsFor(string fromLocal, string toLocal)
    {
        var finder = new VisiblePassFinder(
            new Sgp4SatellitePassPredictor(), new Sgp4SatelliteService(), new AstronomyEngineSunService(), new Sgp4SatelliteIlluminationService());
        var from = Local(fromLocal);
        var passes = finder.Find(TleTests.Iss, Humanes, from, Local(toLocal));
        return _planner.Plan(passes, TleTests.Iss.Epoch, from, Madrid);
    }

    [Fact]
    public void Oct16_HighDawnPass_TenMinutesBeforeItLightsUp()
    {
        // Heavens-Above: lights up at 7:12:53 at 20° SW, peaks at 7:15:05 at 68° SE. Alert time is past the quiet hours.
        var alert = Assert.Single(AlertsFor("2026-10-16 07:00", "2026-10-16 08:00"));

        Assert.False(alert.IsEveningBefore);
        Assert.InRange(alert.NotifyAt, Local("2026-10-16 07:02:40"), Local("2026-10-16 07:03:00"));
        Assert.Equal("La ISS pasa en 10 min", PassAlertText.Title(alert));
        Assert.Matches(@"^A las 7:12 pasa la ISS · \d min · aparece a 19° al SO, máximo 67° al SE$", PassAlertText.Body(alert, Madrid));
    }

    [Fact]
    public void Oct15_LowDawnPass_TheEveningBefore()
    {
        // Heavens-Above: lights up at 6:24:24 at 10° SSE, peaks at 6:25:56 at 13° SE.
        var alert = Assert.Single(AlertsFor("2026-10-14 20:00", "2026-10-15 07:00"));

        Assert.True(alert.IsEveningBefore);
        Assert.Equal(Local("2026-10-14 22:00"), alert.NotifyAt);
        Assert.Equal("Mañana temprano pasa la ISS", PassAlertText.Title(alert));
        Assert.Matches(@"^Mañana a las 6:24 pasa la ISS · \d min · aparece a 10° al SE, máximo 13° al SE$", PassAlertText.Body(alert, Madrid));
    }

    [Fact]
    public void Oct18_TwoPasses_OneInTheEveningOneInTheMorning()
    {
        // Heavens-Above: 5:43 (11° E) and 7:17 (42° NNW). The first would wake you up; the second is announced at about 7:06.
        var alerts = AlertsFor("2026-10-17 12:00", "2026-10-18 09:00");

        Assert.Equal(2, alerts.Count);
        Assert.True(alerts[0].IsEveningBefore);
        Assert.Equal(Local("2026-10-17 22:00"), alerts[0].NotifyAt);
        Assert.False(alerts[1].IsEveningBefore);
        Assert.InRange(alerts[1].NotifyAt, Local("2026-10-18 07:05"), Local("2026-10-18 07:07"));
    }
}
