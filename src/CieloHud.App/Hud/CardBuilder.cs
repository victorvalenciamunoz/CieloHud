using CieloHud.Core.Cards;
using CieloHud.Core.Constellations;
using CieloHud.Core.Sky;
using CieloHud.Core.SolarSystem;

namespace CieloHud.App.Hud;

/// <summary>
/// What a card shows: name, what it is and where, its drawing (null if it has none), its written text (null if not written
/// yet), and its facts under <paramref name="FactsHeader"/>: "AHORA" when they change with the moment, "DATOS" for a star.
/// <paramref name="FactsFirst"/>: a star's facts are three short lines and go before its text, so the folded card shows
/// them all (decision 046); the other cards keep the text first. <paramref name="History"/>: the dated events of the object,
/// oldest first, listed at the end under HISTORIA (decision 052); empty when it has none.
/// </summary>
public sealed record CardView(string Title, string Subtitle, CardPicture? Picture, string? Text, string FactsHeader, IReadOnlyList<string> Now,
    bool FactsFirst, IReadOnlyList<HistoryLine> History);

/// <summary>An entry of HISTORIA as the card lists it: "7 oct 1959" and what happened.</summary>
public sealed record HistoryLine(string Date, string Text);

/// <summary>
/// Puts a card together from Core: the reviewed text (<see cref="CardTexts"/>), the facts of the moment, already in
/// Spanish (<see cref="FactsText"/>), and what the drawing needs. Only picks which facts go with which object.
/// </summary>
public sealed class CardBuilder(ISolarSystemFactsService facts, ISatelliteFactsService satellites)
{
    /// <summary>The seven targets and the stars; a constellation is not a <see cref="SkyTarget"/> and has its own <see cref="Build(Constellation, Observer, DateTimeOffset)"/>.</summary>
    public static bool HasCard(SkyTarget target) => target is BodyTarget or SatelliteTarget or StarTarget;

    /// <param name="constellation">Where it is, with article ("la Ballena"), or null without a location.</param>
    public CardView Build(SkyTarget target, string? constellation, Observer observer, DateTimeOffset now)
    {
        var subtitle = constellation is null ? target.Kind : $"{target.Kind} · en {constellation}";
        var (picture, lines) = Now(target, observer, now);
        var star = target is StarTarget;
        return new CardView(target.Name.ToUpperInvariant(), subtitle, picture, CardTexts.Find(target.Card)?.Body, star ? "DATOS" : "AHORA", lines,
            FactsFirst: star, History(target.Card));
    }

    private static IReadOnlyList<HistoryLine> History(CardKey key) =>
        CardHistory.For(key).Select(e => new HistoryLine(FactsText.HistoryDate(e.Date), e.Text.Body)).ToList();

    /// <summary>
    /// A constellation's card, from «¿qué es?»: its size, how much of it rises from here and its brightest stars, under «DATOS»
    /// and before its text, as on a star's card (decision 047), under its figure as it looks now (decision 049).
    /// </summary>
    public static CardView Build(Constellation constellation, Observer observer, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(constellation);
        var facts = ConstellationFacts.Of(constellation.Symbol, observer);
        var lines = FactsText.Constellation(facts, facts.BrightestStars?.Select(s => SpanishNames.Star(s)).ToList());
        var title = SpanishNames.WithoutArticle(SpanishNames.Constellation(constellation)).ToUpperInvariant();
        var shape = ConstellationShape.Of(constellation.Symbol, observer, now);
        var labels = shape.Stars.Where(s => s.Labelled).ToDictionary(s => s.Star.Designation, s => SpanishNames.Star(s.Star));
        var key = CardKey.Constellation(constellation.Symbol);
        return new CardView(title, "constelación", new ConstellationPicture(shape, labels), CardTexts.Find(key)?.Body,
            "DATOS", lines, FactsFirst: true, History(key));
    }

    private (CardPicture? Picture, IReadOnlyList<string> Lines) Now(SkyTarget target, Observer observer, DateTimeOffset now)
    {
        switch (target)
        {
            case BodyTarget { Body: CelestialBody.Moon }:
                var moon = facts.Moon(observer, now);
                return (new PhasePicture(facts.Disc(CelestialBody.Moon, observer, now)),
                    [FactsText.MoonPhase(moon), FactsText.NextQuarter(moon.Next, now, TimeZoneInfo.Local), FactsText.MoonDistance(moon)]);
            case BodyTarget { Body: CelestialBody.Jupiter }:
                var jupiter = facts.Planet(CelestialBody.Jupiter, observer, now);
                var moons = facts.JupiterMoons(observer, now);
                return (new JupiterPicture(moons),
                    [FactsText.PlanetDistance(jupiter), FactsText.PlanetLight(jupiter), .. FactsText.JupiterMoons(moons)]);
            case BodyTarget { Body: CelestialBody.Saturn }:
                var saturn = facts.Planet(CelestialBody.Saturn, observer, now);
                var rings = facts.SaturnRings(now);
                var pole = facts.Disc(CelestialBody.Saturn, observer, now).NorthPoleDegrees;
                return (new SaturnPicture(SaturnShape.Of(rings.TiltDegrees, pole)),
                    [FactsText.PlanetDistance(saturn), FactsText.PlanetLight(saturn), .. FactsText.SaturnRings(rings, now)]);
            case StarTarget star:
                var color = StarColors.Of(star.Star);
                return (new StarPicture(color),
                    [FactsText.StarLight(star.Name, StarLight.Of(star.Star)), FactsText.StarColorName(color), FactsText.StarMagnitude(star.Star.Magnitude)]);
            case SatelliteTarget { Tle: { } tle }:
                var iss = satellites.Satellite(tle, observer, now);
                return (null, [FactsText.SatelliteAltitude(iss), FactsText.SatelliteSpeed(iss), FactsText.SatelliteVisibility(iss)]);
            case BodyTarget planet:
                var p = facts.Planet(planet.Body, observer, now);
                return (new PhasePicture(facts.Disc(planet.Body, observer, now)), [FactsText.PlanetDistance(p), FactsText.PlanetLight(p)]);
            default:
                // The ISS before its orbit is downloaded (or without network): only its text.
                return (null, []);
        }
    }
}
