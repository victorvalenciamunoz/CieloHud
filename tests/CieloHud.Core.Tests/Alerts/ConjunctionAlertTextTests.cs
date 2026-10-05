using static CieloHud.Core.Tests.Alerts.AlertConjunctions;
using static CieloHud.Core.Tests.Alerts.AlertPasses;

namespace CieloHud.Core.Tests.Alerts;

public class ConjunctionAlertTextTests
{
    private static ConjunctionAlert Alert(string notifyAt, bool eveningBefore, params Conjunction[] conjunctions) =>
        new(conjunctions, Local(notifyAt), eveningBefore);

    private static readonly Conjunction EveningJupiter =
        Conjunction(CelestialBody.Jupiter, "2026-10-14 20:15", "2026-10-14 22:00", "2026-10-15 01:00", separation: 3.2);

    [Fact]
    public void ExampleFromThePlan()
    {
        var alert = Alert("2026-10-14 19:45", false, EveningJupiter);

        Assert.Equal("La Luna junto a Júpiter", ConjunctionAlertText.Title(alert));
        Assert.Equal("Esta noche, la Luna junto a Júpiter (3°) · mejor hacia las 22:00 al SE", ConjunctionAlertText.Body(alert, Madrid));
    }

    [Theory]
    [InlineData("2026-10-14 22:07", "22:00")]
    [InlineData("2026-10-14 22:08", "22:15")]  // 7.5 min rounds up
    [InlineData("2026-10-14 23:55", "0:00")]
    public void BestTime_RoundedToTheQuarterHour(string best, string expected)
    {
        var jupiter = Conjunction(CelestialBody.Jupiter, "2026-10-14 20:15", best, "2026-10-15 01:00");

        Assert.EndsWith($"mejor hacia las {expected} al SE", ConjunctionAlertText.Body(Alert("2026-10-14 19:45", false, jupiter), Madrid));
    }

    [Fact]
    public void ShortWindow_RoundingWouldFallOutside_ExactTime()
    {
        // Like 27 Dec 2026: visible from 23:20 to 23:45, best at 23:20; "hacia las 23:15" would be before it can be seen.
        var jupiter = Conjunction(CelestialBody.Jupiter, "2026-12-27 23:20", "2026-12-27 23:20", "2026-12-27 23:45");

        Assert.EndsWith("mejor hacia las 23:20 al SE", ConjunctionAlertText.Body(Alert("2026-12-27 22:50", false, jupiter), Madrid));
    }

    [Theory]
    [InlineData(0.4, "menos de 1°")]
    [InlineData(1.5, "2°")]
    [InlineData(4.49, "4°")]
    public void Separation_WholeDegrees(double separation, string expected)
    {
        var jupiter = Conjunction(CelestialBody.Jupiter, "2026-10-14 20:15", "2026-10-14 22:00", "2026-10-15 01:00", separation);

        Assert.Contains($"Júpiter ({expected})", ConjunctionAlertText.Body(Alert("2026-10-14 19:45", false, jupiter), Madrid));
    }

    [Fact]
    public void EveningBefore_SmallHours()
    {
        var jupiter = Conjunction(CelestialBody.Jupiter, "2026-11-03 02:35", "2026-11-03 03:30", "2026-11-03 07:15", separation: 3.1, moonAzimuth: 91);

        var body = ConjunctionAlertText.Body(Alert("2026-11-02 22:00", true, jupiter), Madrid);

        Assert.Equal("Esta madrugada, la Luna junto a Júpiter (3°) · mejor hacia las 3:30 al E", body);
    }

    [Fact]
    public void EveningBefore_Dawn()
    {
        var jupiter = Conjunction(CelestialBody.Jupiter, "2026-11-30 00:45", "2026-11-30 07:45", "2026-11-30 07:45", separation: 1.9, moonAzimuth: 216);

        var body = ConjunctionAlertText.Body(Alert("2026-11-29 22:00", true, jupiter), Madrid);

        Assert.Equal("Mañana temprano, la Luna junto a Júpiter (2°) · mejor hacia las 7:45 al SO", body);
    }

    [Fact]
    public void SameMorning_AfterTheQuietHours()
    {
        var venus = Conjunction(CelestialBody.Venus, "2026-11-07 07:45", "2026-11-07 07:50", "2026-11-07 07:55", separation: 2, moonAzimuth: 119);

        var body = ConjunctionAlertText.Body(Alert("2026-11-07 07:15", false, venus), Madrid);

        Assert.StartsWith("Esta mañana, la Luna junto a Venus (2°)", body);
    }

    [Fact]
    public void WindowAlreadyOpen_Now()
    {
        var body = ConjunctionAlertText.Body(Alert("2026-10-14 21:00", false, EveningJupiter), Madrid);

        Assert.Equal("Ahora, la Luna junto a Júpiter (3°) · mejor hacia las 22:00 al SE", body);
    }

    [Fact]
    public void PastTheBestMoment_NowUntilTheEnd()
    {
        var body = ConjunctionAlertText.Body(Alert("2026-10-14 23:10", false, EveningJupiter), Madrid);

        Assert.Equal("Ahora, la Luna junto a Júpiter (3°) · al SO, hasta la 1:00", body);
    }

    [Fact]
    public void TwoPlanets_ClosestFirst_BestMomentOfTheClosest()
    {
        var mars = Conjunction(CelestialBody.Mars, "2026-10-14 21:25", "2026-10-14 23:35", "2026-10-15 04:10", separation: 3.4, moonAzimuth: 220);
        var jupiter = Conjunction(CelestialBody.Jupiter, "2026-10-14 21:25", "2026-10-14 21:25", "2026-10-15 00:45", separation: 3.6);
        var alert = Alert("2026-10-14 20:55", false, mars, jupiter);

        Assert.Equal("La Luna junto a Marte y Júpiter", ConjunctionAlertText.Title(alert));
        Assert.Equal("Esta noche, la Luna junto a Marte (3°) y Júpiter (4°) · mejor hacia las 23:30 al SO", ConjunctionAlertText.Body(alert, Madrid));
    }

    [Fact]
    public void ThreePlanets_CommasAndY()
    {
        var alert = Alert("2026-10-14 19:45", false,
            Conjunction(CelestialBody.Venus, "2026-10-14 20:15", "2026-10-14 20:30", "2026-10-14 21:00", separation: 1),
            Conjunction(CelestialBody.Jupiter, "2026-10-14 20:15", "2026-10-14 21:00", "2026-10-14 23:00", separation: 2),
            Conjunction(CelestialBody.Saturn, "2026-10-14 20:15", "2026-10-14 21:30", "2026-10-14 23:30", separation: 4));

        Assert.Equal("La Luna junto a Venus, Júpiter y Saturno", ConjunctionAlertText.Title(alert));
    }

    [Fact]
    public void LocalTime_AfterTheTimeChange()
    {
        // 25 Oct 2026, 3:00 CEST → 2:00 CET. The best moment at 20:00 UTC is 21:00 local.
        var jupiter = Conjunction(CelestialBody.Jupiter, "2026-10-26 19:00", "2026-10-26 21:00", "2026-10-26 23:00");

        Assert.Contains("hacia las 21:00", ConjunctionAlertText.Body(Alert("2026-10-26 18:30", false, jupiter), Madrid));
        Assert.Equal(new DateTimeOffset(2026, 10, 26, 20, 0, 0, TimeSpan.Zero), jupiter.Best.Instant);
    }

    [Theory]
    [InlineData(CelestialBody.Mercury, "Mercurio")]
    [InlineData(CelestialBody.Venus, "Venus")]
    [InlineData(CelestialBody.Mars, "Marte")]
    [InlineData(CelestialBody.Jupiter, "Júpiter")]
    [InlineData(CelestialBody.Saturn, "Saturno")]
    public void PlanetNames_InSpanish(CelestialBody planet, string name)
    {
        Assert.Equal(name, ConjunctionAlertText.PlanetName(planet));
    }

    [Fact]
    public void Moon_IsNotAPlanetName()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ConjunctionAlertText.PlanetName(CelestialBody.Moon));
    }
}
