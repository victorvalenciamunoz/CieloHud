using static CieloHud.Core.Tests.Alerts.AlertPasses;

namespace CieloHud.Core.Tests.Alerts;

/// <summary>
/// The conjunctions found with the real ephemeris from Madrid (validated against JPL Horizons in
/// <see cref="Conjunctions.ConjunctionFinderIntegrationTests"/>) turned into alerts, planned on 5 Oct 2026.
/// </summary>
public class ConjunctionAlertIntegrationTests
{
    private static readonly Observer MadridCenter = new(latitudeDegrees: 40.4168, longitudeDegrees: -3.7038, altitudeMeters: 650);
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 10, 0, 0, TimeSpan.Zero);

    private static readonly Lazy<IReadOnlyList<ConjunctionAlert>> Alerts = new(() =>
    {
        var finder = new ConjunctionFinder(new AstronomyEngineSolarSystemService(), new AstronomyEngineSunService());
        var conjunctions = finder.FindWithMoon(MadridCenter, Now, Now.AddDays(120), Madrid);
        return new ConjunctionAlertPlanner().Plan(conjunctions, Now, Madrid);
    });

    [Fact]
    public void Season_AlertTimesAndTexts()
    {
        string[] expected =
        [
            "2026-10-05 22:00 | Mañana temprano, la Luna junto a Júpiter (2°) · mejor hacia las 7:45 al E",
            "2026-11-01 22:00 | Mañana temprano, la Luna junto a Marte (4°) · mejor hacia las 7:15 al S",
            "2026-11-02 22:00 | Esta madrugada, la Luna junto a Júpiter (3°) · mejor hacia las 3:30 al E",
            "2026-11-06 22:00 | Mañana temprano, la Luna junto a Venus (2°) · mejor hacia las 7:15 al SE",
            "2026-11-29 22:00 | Mañana temprano, la Luna junto a Júpiter (2°) · mejor hacia las 7:45 al SO",
            "2026-12-27 22:50 | Esta noche, la Luna junto a Júpiter (5°) · mejor hacia las 23:20 al E",
            "2027-01-23 20:25 | Esta noche, la Luna junto a Júpiter (2°) · mejor hacia las 23:00 al E",
        ];

        var actual = Alerts.Value.Select(a =>
            $"{TimeZoneInfo.ConvertTime(a.NotifyAt, Madrid):yyyy-MM-dd HH:mm} | {ConjunctionAlertText.Body(a, Madrid)}");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Season_EveryAlertBeforeItsWindowAndOutsideTheQuietHours()
    {
        var planner = new AlertPlanner();
        foreach (var alert in Alerts.Value)
        {
            Assert.True(alert.NotifyAt < alert.WindowStart);
            Assert.False(planner.IsQuiet(alert.NotifyAt, Madrid));
        }
    }
}
