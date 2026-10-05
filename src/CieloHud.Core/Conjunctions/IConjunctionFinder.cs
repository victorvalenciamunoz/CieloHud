using CieloHud.Core.Sky;

namespace CieloHud.Core.Conjunctions;

/// <summary>Finds the nights when the Moon passes close to a bright planet, as seen from the observer.</summary>
public interface IConjunctionFinder
{
    /// <summary>
    /// Conjunctions whose window starts in [<paramref name="from"/>, <paramref name="to"/>), ordered by their best moment.
    /// A window already open at <paramref name="from"/> is cut there; one still open at <paramref name="to"/> is returned whole.
    /// </summary>
    /// <param name="timeZone">The observer's: the best moment prefers the evening, before local midnight.</param>
    IReadOnlyList<MoonPlanetConjunction> Find(Observer observer, DateTimeOffset from, DateTimeOffset to, TimeZoneInfo timeZone);
}
