using System.Globalization;
using System.Text;
using CieloHud.Core.Cards;
using CieloHud.Core.Sky;
using CieloHud.Core.SolarSystem;

namespace CieloHud.Core.Stars;

/// <summary>A star offered by the STARS chip: its Spanish name, its constellation ("la Lira") and where it is now.</summary>
public sealed record StarChoice(Star Star, string Name, string Constellation, HorizontalPosition Position)
{
    /// <summary>Below the horizon the guide does not lead to it (it says so instead); the list marks it.</summary>
    public bool AboveHorizon => Position.AltitudeDegrees > 0;
}

/// <summary>What a search found: the first <see cref="StarChoices.MaxShown"/> in alphabetical order, and how many matched.</summary>
public sealed record StarSearchResult(IReadOnlyList<StarChoice> Shown, int Total)
{
    /// <summary>More matched than are shown: the user should keep typing.</summary>
    public bool HasMore => Total > Shown.Count;
}

/// <summary>
/// How a star is chosen as the target of the guide (decision 054): a short list of the brightest ones up now, and a search
/// over the whole catalog. Never more than <see cref="MaxShown"/> at once, always in alphabetical order, which is how a name
/// is looked for.
/// </summary>
public sealed class StarChoices
{
    /// <summary>With more than ten items the user gets lost.</summary>
    public const int MaxShown = 10;

    /// <summary>The short list leaves out stars lower than this: near the horizon they fade or hide behind buildings.</summary>
    public const double MinAltitudeDegrees = 10;

    /// <summary>Above this the Sun is up or the sky is still too bright for stars: civil twilight, as for the ISS passes.</summary>
    public const double MaxSunAltitudeDegrees = -6;

    private readonly IStarService _stars;
    private readonly ISunService _sun;
    private readonly IReadOnlyList<Star> _catalog;

    public StarChoices(IStarService stars, ISunService sun, IReadOnlyList<Star>? catalog = null)
    {
        _stars = stars;
        _sun = sun;
        _catalog = catalog ?? BrightStars.All;
    }

    /// <summary>
    /// The <see cref="MaxShown"/> brightest stars higher than <see cref="MinAltitudeDegrees"/>, in alphabetical order:
    /// brightness picks which ones, the order is for finding them in the list. Empty if none is that high.
    /// </summary>
    public IReadOnlyList<StarChoice> Brightest(Observer observer, DateTimeOffset instant) =>
        Alphabetical(_catalog
            .Select(s => Choice(s, observer, instant))
            .Where(c => c.Position.AltitudeDegrees > MinAltitudeDegrees)
            .OrderBy(c => c.Star.Magnitude)
            .Take(MaxShown));

    /// <summary>
    /// The stars whose names match <paramref name="query"/> (see <see cref="Matches"/>), above or below the horizon, in
    /// alphabetical order; at most <see cref="MaxShown"/> of them, with the total. A blank query matches nothing.
    /// </summary>
    public StarSearchResult Search(string query, Observer observer, DateTimeOffset instant)
    {
        ArgumentNullException.ThrowIfNull(query);
        var found = Alphabetical(_catalog.Where(s => Matches(s, query)).Select(s => Choice(s, observer, instant)));
        return new StarSearchResult(found.Take(MaxShown).ToList(), found.Count);
    }

    /// <summary>The Sun is higher than <see cref="MaxSunAltitudeDegrees"/>: the stars cannot be seen.</summary>
    public bool IsDaylight(Observer observer, DateTimeOffset instant) =>
        _sun.Locate(observer, instant).AltitudeDegrees > MaxSunAltitudeDegrees;

    /// <summary>
    /// Every word of the query starts a word of the star's Spanish name or its IAU name, ignoring accents and case:
    /// "casi" finds "Gamma de Casiopea", "betel" Betelgeuse, "sirius" Sirio. Not its designation: every "alf …" would match
    /// "al", and the names are what the app shows. A blank query matches nothing.
    /// </summary>
    public static bool Matches(Star star, string query)
    {
        ArgumentNullException.ThrowIfNull(star);
        ArgumentNullException.ThrowIfNull(query);
        var wanted = Words(query);
        if (wanted.Count == 0)
            return false;
        var words = Words($"{SpanishNames.Star(star)} {star.ProperName}");
        return wanted.All(w => words.Any(word => word.StartsWith(w, StringComparison.Ordinal)));
    }

    /// <summary>What a row says after the name: "en la Lira · 47° al O", or "en el Can Mayor · bajo el horizonte".</summary>
    public static string Detail(StarChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        var where = choice.AboveHorizon
            ? $"{Alerts.AlertWords.Round(choice.Position.AltitudeDegrees).ToString(CultureInfo.InvariantCulture)}° al {Alerts.AlertWords.Cardinal(choice.Position.CardinalPoint)}"
            : "bajo el horizonte";
        return $"en {choice.Constellation} · {where}";
    }

    /// <summary>The Spanish name of the constellation in the star's Bayer designation ("alf Lyr" → "la Lira").</summary>
    public static string ConstellationOf(Star star)
    {
        ArgumentNullException.ThrowIfNull(star);
        var parts = star.Designation.Split(' ', 2);
        return parts.Length == 2 && SpanishNames.Constellations.TryGetValue(parts[1], out var name)
            ? name
            : throw new ArgumentException($"'{star.Designation}' is not a Bayer designation with an IAU constellation.", nameof(star));
    }

    /// <summary>Lower case, without accents or punctuation: "Épsilon del Cisne" → "epsilon del cisne".</summary>
    public static string Fold(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var folded = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            folded.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return folded.ToString();
    }

    private StarChoice Choice(Star star, Observer observer, DateTimeOffset instant) =>
        new(star, SpanishNames.Star(star), ConstellationOf(star), _stars.Locate(star, observer, instant));

    /// <summary>By the name without accents, so "Épsilon" goes with the E, the same on every device whatever its culture.</summary>
    private static List<StarChoice> Alphabetical(IEnumerable<StarChoice> choices) =>
        choices.OrderBy(c => Fold(c.Name), StringComparer.Ordinal).ThenBy(c => c.Star.Designation, StringComparer.Ordinal).ToList();

    private static List<string> Words(string text) =>
        Fold(text).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
}
