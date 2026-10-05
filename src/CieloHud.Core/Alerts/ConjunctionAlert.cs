using CieloHud.Core.Conjunctions;
using CieloHud.Core.SolarSystem;

namespace CieloHud.Core.Alerts;

/// <summary>
/// The Moon next to one planet, or to several at once (their windows overlap), and when to announce it.
/// </summary>
/// <param name="Conjunctions">Closest first. Never empty.</param>
/// <param name="NotifyAt">When the alert goes off (UTC). Equal to the planning instant when it is already due.</param>
/// <param name="IsEveningBefore">True when it would be announced in the quiet hours and is moved to the evening before.</param>
public sealed record ConjunctionAlert(IReadOnlyList<Conjunction> Conjunctions, DateTimeOffset NotifyAt, bool IsEveningBefore)
{
    /// <summary>The closest planet: its best moment is the one the alert recommends.</summary>
    public Conjunction Closest => Conjunctions[0];

    /// <summary>When the first window opens.</summary>
    public DateTimeOffset WindowStart => Conjunctions.Min(c => c.Start.Instant);

    /// <summary>When the last window closes: the alert is pointless after it.</summary>
    public DateTimeOffset WindowEnd => Conjunctions.Max(c => c.End.Instant);
}

/// <summary>A conjunction already announced: its planet and best moment, enough to recognize it when planning again.</summary>
public readonly record struct NotifiedConjunction(CelestialBody Planet, DateTimeOffset Best)
{
    public static NotifiedConjunction From(Conjunction conjunction) => new(conjunction.Companion, conjunction.Best.Instant);
}
