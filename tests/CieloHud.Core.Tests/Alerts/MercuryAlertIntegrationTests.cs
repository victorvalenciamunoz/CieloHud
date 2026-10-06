using static CieloHud.Core.Tests.Alerts.AlertPasses;

namespace CieloHud.Core.Tests.Alerts;

/// <summary>
/// The seasons of Mercury found with the real ephemeris from Madrid (validated against JPL Horizons in
/// <see cref="Apparitions.MercuryApparitionFinderIntegrationTests"/>), turned into alerts, planned on 6 Oct 2026 for a year.
/// </summary>
public class MercuryAlertIntegrationTests
{
    private static readonly Observer MadridCenter = new(latitudeDegrees: 40.4168, longitudeDegrees: -3.7038, altitudeMeters: 650);
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 10, 0, 0, TimeSpan.Zero);

    private static readonly Lazy<IReadOnlyList<MercuryAlert>> Alerts = new(() =>
    {
        var finder = new MercuryApparitionFinder(
            new AstronomyEngineSolarSystemService(), new AstronomyEngineSunService(), new AstronomyEngineMagnitudeService());
        return new MercuryAlertPlanner().Plan(finder.Find(MadridCenter, Now, Now.AddDays(365), Madrid), Now, Madrid);
    });

    [Fact]
    public void Year_AlertTimesAndTexts()
    {
        string[] expected =
        [
            "2026-11-19 22:00 | Mercurio al amanecer | Mañana al amanecer, Mercurio a 12°, lo más alto en estas semanas · mejor hacia las 7:35 al SE · se ve del 14 al 28 nov",
            "2027-02-04 18:35 | Mercurio al anochecer | Hoy al anochecer, Mercurio a 11°, lo más alto en estas semanas · mejor hacia las 19:05 al SO · se ve del 31 ene al 7 feb",
            "2027-05-26 21:36 | Mercurio al anochecer | Hoy al anochecer, Mercurio a 13°, lo más alto en estas semanas · mejor hacia las 22:10 al O · se ve del 15 al 28 may",
        ];

        var actual = Alerts.Value.Select(a =>
            $"{TimeZoneInfo.ConvertTime(a.NotifyAt, Madrid):yyyy-MM-dd HH:mm} | {MercuryAlertText.Title(a)} | {MercuryAlertText.Body(a, Madrid)}");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Year_EveryAlertBeforeItsWindowAndOutsideTheQuietHours()
    {
        var planner = new AlertPlanner();
        foreach (var alert in Alerts.Value)
        {
            Assert.True(alert.NotifyAt < alert.WindowStart);
            Assert.False(planner.IsQuiet(alert.NotifyAt, Madrid));
        }
    }
}
