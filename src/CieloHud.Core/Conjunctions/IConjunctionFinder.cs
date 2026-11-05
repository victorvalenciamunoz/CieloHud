using CieloHud.Core.Sky;

namespace CieloHud.Core.Conjunctions;

/// <summary>Finds the nights when the Moon passes close to a bright planet, or two planets are close together, as seen from the observer.</summary>
public interface IConjunctionFinder
{
    /// <summary>
    /// The Moon next to a planet: conjunctions whose window starts in [<paramref name="from"/>, <paramref name="to"/>), ordered by
    /// their best moment. A window already open at <paramref name="from"/> is cut there; one still open at <paramref name="to"/> is returned whole.
    /// </summary>
    /// <param name="timeZone">The observer's: the best moment prefers the evening, before local midnight.</param>
    IReadOnlyList<Conjunction> FindWithMoon(Observer observer, DateTimeOffset from, DateTimeOffset to, TimeZoneInfo timeZone);

    /// <summary>
    /// Two planets together: one conjunction per approach, on its closest night, when that night's window overlaps
    /// [<paramref name="from"/>, <paramref name="to"/>). Ordered by best moment. <see cref="Conjunction.Nights"/> tells all the nights they are close.
    /// </summary>
    IReadOnlyList<Conjunction> FindPlanetPairs(Observer observer, DateTimeOffset from, DateTimeOffset to, TimeZoneInfo timeZone);
}
