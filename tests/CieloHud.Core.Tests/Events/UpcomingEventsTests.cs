using static CieloHud.Core.Tests.Alerts.AlertConjunctions;
using static CieloHud.Core.Tests.Alerts.AlertPasses;

namespace CieloHud.Core.Tests.Events;

public class UpcomingEventsTests
{
    private static readonly DateTimeOffset Now = Local("2026-10-14 09:00");
    private static readonly DateTimeOffset FreshEpoch = Local("2026-10-13 12:00");

    private static IReadOnlyList<SkyEvent> Build(IEnumerable<VisiblePass> passes, IEnumerable<Conjunction> conjunctions,
        IEnumerable<DateTimeOffset>? notifiedPasses = null, IEnumerable<NotifiedConjunction>? notifiedConjunctions = null) =>
        UpcomingEvents.Build(passes, FreshEpoch, conjunctions, Now, Madrid, notifiedPasses, notifiedConjunctions);

    [Fact]
    public void Pass_TimeTitleDetailsAndAlert()
    {
        var e = Assert.Single(Build([Pass(Local("2026-10-14 21:43"))], []));

        Assert.Equal(SkyEventKind.Pass, e.Kind);
        Assert.Equal("ISS", e.Target);
        Assert.Equal("21:43", e.Time);
        Assert.Equal("Pasa la ISS", e.Title);
        Assert.Equal("5 min · aparece por el NO, máximo 67° al SE", e.Details);
        Assert.Equal(Local("2026-10-14 21:33"), e.AlertAt);
    }

    [Fact]
    public void MoonConjunction_ApproximateTimeAndGuideDirection()
    {
        var jupiter = Conjunction(CelestialBody.Jupiter, "2026-10-14 20:15", "2026-10-14 22:07", "2026-10-15 01:00", separation: 2.4);

        var e = Assert.Single(Build([], [jupiter]));

        Assert.Equal(SkyEventKind.MoonConjunction, e.Kind);
        Assert.Equal("Luna", e.Target);
        Assert.Equal("≈22:00", e.Time);
        Assert.Equal("La Luna junto a Júpiter (2°)", e.Title);
        Assert.Equal("al SE", e.Details);
        Assert.Equal(Local("2026-10-14 19:45"), e.AlertAt);
    }

    [Fact]
    public void PlanetPair_NightsTogetherAndBrighterAsTarget()
    {
        var marsJupiter = PlanetPair(CelestialBody.Jupiter, CelestialBody.Mars,
            "2026-11-16 01:35", "2026-11-16 07:25", "2026-11-16 07:30", "2026-11-09 02:00", "2026-11-23 01:20");

        var e = Assert.Single(Build([], [marsJupiter]));

        Assert.Equal(SkyEventKind.PlanetPair, e.Kind);
        Assert.Equal("Júpiter", e.Target);
        Assert.Equal("≈7:30", e.Time);
        Assert.Equal("Marte junto a Júpiter (1°)", e.Title);
        Assert.Equal("al S · juntos del 9 al 23 nov", e.Details);
        Assert.Equal(Local("2026-11-15 22:00"), e.AlertAt);
    }

    [Fact]
    public void Mixed_OrderedByWhenToLook()
    {
        var jupiter = Conjunction(CelestialBody.Jupiter, "2026-10-14 20:15", "2026-10-14 22:00", "2026-10-15 01:00");

        var events = Build([Pass(Local("2026-10-15 06:25")), Pass(Local("2026-10-14 21:43"))], [jupiter]);

        Assert.Equal(["21:43", "≈22:00", "6:25"], events.Select(e => e.Time));
    }

    [Fact]
    public void AlreadyAnnounced_ShownWithoutAlert()
    {
        var pass = Pass(Local("2026-10-14 21:43"));
        var jupiter = Conjunction(CelestialBody.Jupiter, "2026-10-14 20:15", "2026-10-14 22:00", "2026-10-15 01:00");

        var events = Build([pass], [jupiter], [pass.VisibleStart.Instant], [NotifiedConjunction.From(jupiter)]);

        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.Null(e.AlertAt));
    }

    [Fact]
    public void PassUnderWay_ShownWithoutAlert_FinishedLeftOut()
    {
        var underWay = Pass(Local("2026-10-14 08:57"));  // visible 8:57-9:02
        var over = Pass(Local("2026-10-14 08:50"));      // visible 8:50-8:55

        var e = Assert.Single(Build([underWay, over], []));

        Assert.Equal("8:57", e.Time);
        Assert.Null(e.AlertAt);
    }

    [Fact]
    public void NoOrbit_ConjunctionsStillListed()
    {
        var jupiter = Conjunction(CelestialBody.Jupiter, "2026-10-14 20:15", "2026-10-14 22:00", "2026-10-15 01:00");

        var e = Assert.Single(UpcomingEvents.Build([], null, [jupiter], Now, Madrid));

        Assert.Equal("Luna", e.Target);
    }

    [Theory]
    [InlineData("2026-10-14 23:59", "Hoy")]
    [InlineData("2026-10-15 00:00", "Mañana")]
    [InlineData("2026-10-16 07:30", "vie 16 oct")]
    [InlineData("2026-11-16 07:25", "lun 16 nov")]
    public void DayLabel_InLocalTime(string local, string expected)
    {
        Assert.Equal(expected, UpcomingEvents.DayLabel(Local(local), Now, Madrid));
    }

    [Fact]
    public void DayLabel_AcrossTheTimeChange()
    {
        // 25 Oct 2026: 23:30 local on the 25th is 22:30 UTC; it is still the 25th in Madrid.
        var now = Local("2026-10-24 12:00");

        Assert.Equal("Mañana", UpcomingEvents.DayLabel(new DateTimeOffset(2026, 10, 25, 22, 30, 0, TimeSpan.Zero), now, Madrid));
    }

    [Theory]
    [InlineData("2026-10-14 22:00", "Aviso hoy a las 22:00")]
    [InlineData("2026-10-15 01:05", "Aviso mañana a la 1:05")]
    [InlineData("2026-11-15 22:00", "Aviso el dom 15 nov a las 22:00")]
    public void AlertLabel_InWords(string local, string expected)
    {
        Assert.Equal(expected, UpcomingEvents.AlertLabel(Local(local), Now, Madrid));
    }
}
