using CieloHud.Core.Conjunctions;
using CieloHud.Core.SolarSystem;

namespace CieloHud.Core.Alerts;

/// <summary>
/// What a conjunction alert announces, and when: the Moon next to one planet, or to several at once (their windows overlap);
/// or two planets together, on the closest night of their approach.
/// </summary>
/// <param name="Conjunctions">Closest first. Never empty. Two planets always come alone.</param>
/// <param name="NotifyAt">When the alert goes off (UTC). Equal to the planning instant when it is already due.</param>
/// <param name="IsEveningBefore">True when it would be announced in the quiet hours and is moved to the evening before.</param>
public sealed record ConjunctionAlert(IReadOnlyList<Conjunction> Conjunctions, DateTimeOffset NotifyAt, bool IsEveningBefore)
{
    /// <summary>The closest one: its best moment is the one the alert recommends.</summary>
    public Conjunction Closest => Conjunctions[0];

    /// <summary>What the HUD guides to when the alert is tapped: the Moon, or the brighter planet.</summary>
    public CelestialBody Guide => Closest.Guide;

    /// <summary>When the first window opens.</summary>
    public DateTimeOffset WindowStart => Conjunctions.Min(c => c.Start.Instant);

    /// <summary>When the last window closes: the alert is pointless after it.</summary>
    public DateTimeOffset WindowEnd => Conjunctions.Max(c => c.End.Instant);
}

/// <summary>A conjunction already announced, enough to recognize it when planning again.</summary>
/// <param name="Planet">The companion: the planet next to the Moon, or the fainter of two planets.</param>
/// <param name="Guide">The Moon, or the brighter planet. Last, defaulting to the Moon, so those stored before two planets existed still read.</param>
public readonly record struct NotifiedConjunction(CelestialBody Planet, DateTimeOffset Best, CelestialBody Guide = CelestialBody.Moon)
{
    public static NotifiedConjunction From(Conjunction conjunction) => new(conjunction.Companion, conjunction.Best.Instant, conjunction.Guide);
}
