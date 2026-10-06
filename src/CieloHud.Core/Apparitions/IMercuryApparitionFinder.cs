using CieloHud.Core.Sky;

namespace CieloHud.Core.Apparitions;

/// <summary>Finds the days when Mercury can be seen low in the twilight, as seen from the observer.</summary>
public interface IMercuryApparitionFinder
{
    /// <summary>
    /// Seasons of Mercury whose best day's window overlaps [<paramref name="from"/>, <paramref name="to"/>), ordered by their best moment.
    /// Each comes whole, with its first and last days, even if they fall outside the range.
    /// </summary>
    /// <param name="timeZone">The observer's: days are counted, and dusk told from dawn, in local time.</param>
    IReadOnlyList<MercuryApparition> Find(Observer observer, DateTimeOffset from, DateTimeOffset to, TimeZoneInfo timeZone);

    /// <summary>Every twilight window whose start lies in [<paramref name="from"/>, <paramref name="to"/>), day by day, in order.</summary>
    IReadOnlyList<MercuryWindow> FindWindows(Observer observer, DateTimeOffset from, DateTimeOffset to, TimeZoneInfo timeZone);
}
