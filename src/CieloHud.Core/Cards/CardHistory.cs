using System.Globalization;

namespace CieloHud.Core.Cards;

/// <summary>
/// One dated event in the history of an object of the app: a discovery, a mission, something seen in the sky (decisions 051, 052).
/// </summary>
/// <param name="Date">The day as its source gives it: in UTC for missions, in the Julian calendar before 1582 (the source says so).</param>
/// <param name="Card">The object whose card shows it.</param>
/// <param name="Text">What happened. The title is for whoever reviews the file; the card shows the date and the body.</param>
public sealed record HistoryEntry(DateOnly Date, CardKey Card, CardText Text);

/// <summary>
/// The HISTORIA section of the cards: a few dated events per object, always shown, oldest first. One Markdown file per event
/// under <c>Cards/Texts/es/history/&lt;folder&gt;/&lt;yyyy-MM-dd&gt;-&lt;id&gt;.md</c>, with the format of the card texts (decision 037),
/// written and reviewed one by one. An object without entries has no section.
/// </summary>
public static class CardHistory
{
    /// <summary>One or two sentences per entry: several of them go in a card that already has its text and its facts.</summary>
    public const int MaxBodyLength = 200;

    /// <summary>Few and good: the section must not take over the card.</summary>
    public const int MaxPerObject = 4;

    /// <summary>The first day of the Gregorian calendar. Earlier dates are Julian, as their sources and anniversaries give them.</summary>
    public static readonly DateOnly GregorianStart = new(1582, 10, 15);

    internal const string Folder = "history/";

    private static readonly Lazy<IReadOnlyList<HistoryEntry>> Loaded = new(Load);

    /// <summary>Every entry, by object and then oldest first.</summary>
    public static IReadOnlyList<HistoryEntry> All => Loaded.Value;

    /// <summary>The entries of an object, oldest first; none if it has no history.</summary>
    public static IReadOnlyList<HistoryEntry> For(CardKey key) => Loaded.Value.Where(e => e.Card == key).ToList();

    /// <summary>"targets/1610-01-07-Jupiter.md" → 7 Jan 1610, Jupiter; "stars/1862-01-31-alf-CMa.md" → the star "alf CMa".</summary>
    public static (DateOnly Date, CardKey Card) FromPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var parts = path.Split('/');
        if (parts.Length != 2 || !parts[1].EndsWith(".md", StringComparison.Ordinal) || parts[1].Length < "yyyy-MM-dd-x.md".Length
            || parts[1][10] != '-')
            throw new FormatException($"History entry '{path}' is not '<folder>/<yyyy-MM-dd>-<id>.md'.");
        if (!DateOnly.TryParseExact(parts[1][..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new FormatException($"History entry '{path}' does not start with a valid date.");
        return (date, new CardKey(CardTexts.KindOfFolder(parts[0]), parts[1][11..^3].Replace('-', ' ')));
    }

    private static IReadOnlyList<HistoryEntry> Load()
    {
        var assembly = typeof(CardHistory).Assembly;
        var prefix = CardTexts.ResourcePrefix + Folder;
        var entries = new List<HistoryEntry>();
        foreach (var name in assembly.GetManifestResourceNames())
        {
            var path = name.Replace('\\', '/');
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            try
            {
                var (date, card) = FromPath(path[prefix.Length..]);
                entries.Add(new HistoryEntry(date, card, CardTexts.Parse(CardTexts.Read(assembly, name))));
            }
            catch (FormatException ex)
            {
                throw new FormatException($"History entry '{path}': {ex.Message}", ex);
            }
        }
        return entries.OrderBy(e => e.Card.Kind).ThenBy(e => e.Card.Id, StringComparer.Ordinal).ThenBy(e => e.Date).ToList();
    }
}
