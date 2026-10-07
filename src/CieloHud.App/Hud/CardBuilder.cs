using CieloHud.Core.Cards;
using CieloHud.Core.Sky;
using CieloHud.Core.SolarSystem;

namespace CieloHud.App.Hud;

/// <summary>What a card shows: name, what it is and where, its written text (null if not written yet) and the facts of now.</summary>
public sealed record CardView(string Title, string Subtitle, string? Text, IReadOnlyList<string> Now);

/// <summary>
/// Puts a card together from Core: the reviewed text (<see cref="CardTexts"/>) and the facts of the moment, already in
/// Spanish (<see cref="FactsText"/>). Only picks which facts go with which object.
/// </summary>
public sealed class CardBuilder(ISolarSystemFactsService facts)
{
    /// <summary>The seven targets have cards; stars and constellations get theirs in later steps.</summary>
    public static bool HasCard(SkyTarget target) => target is BodyTarget or SatelliteTarget;

    /// <param name="constellation">Where it is, with article ("la Ballena"), or null without a location.</param>
    public CardView Build(SkyTarget target, string? constellation, Observer observer, DateTimeOffset now)
    {
        var subtitle = constellation is null ? target.Kind : $"{target.Kind} · en {constellation}";
        return new CardView(target.Name.ToUpperInvariant(), subtitle, CardTexts.Find(target.Card)?.Body, Now(target, observer, now));
    }

    private IReadOnlyList<string> Now(SkyTarget target, Observer observer, DateTimeOffset now)
    {
        switch (target)
        {
            case BodyTarget { Body: CelestialBody.Moon }:
                var moon = facts.Moon(observer, now);
                return [FactsText.MoonPhase(moon), FactsText.NextQuarter(moon.Next, now, TimeZoneInfo.Local), FactsText.MoonDistance(moon)];
            case BodyTarget planet:
                var p = facts.Planet(planet.Body, observer, now);
                return [FactsText.PlanetDistance(p), FactsText.PlanetLight(p)];
            default:
                // The ISS's height and speed come in step 6.
                return [];
        }
    }
}
