using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using CieloHud.Core.SolarSystem;
using CieloHud.Core.Stars;

namespace CieloHud.Core.Cards;

public enum CardKind
{
    /// <summary>Something the HUD guides to: the Moon, a planet or the ISS.</summary>
    Target,
    Star,
    Constellation,
}

/// <summary>
/// Which card: a target by <see cref="CelestialBody"/> name or <see cref="IssId"/>, a star by its Bayer designation
/// ("alf CMa"), a constellation by its IAU symbol ("Ori").
/// </summary>
public readonly record struct CardKey(CardKind Kind, string Id)
{
    public const string IssId = "ISS";

    public static CardKey Body(CelestialBody body) => new(CardKind.Target, body.ToString());

    public static CardKey Iss => new(CardKind.Target, IssId);

    public static CardKey Star(Star star) => new(CardKind.Star, star.Designation);

    public static CardKey Constellation(string symbol) => new(CardKind.Constellation, symbol);
}

/// <summary>A card's written text (decision 037).</summary>
/// <param name="Title">The object's name, for whoever reviews the file; the app shows the name it already uses.</param>
/// <param name="Body">What the card says, as plain text; paragraphs separated by a blank line.</param>
/// <param name="Sources">Where each fixed fact comes from. Not shown in the app.</param>
public sealed record CardText(string Title, string Body, IReadOnlyList<string> Sources);

/// <summary>
/// The written texts of the cards: one Markdown file per object under <c>Cards/Texts/es</c>, embedded in the assembly,
/// drafted during development and reviewed one by one before they are committed (decision 020). An object without a file
/// has no text yet: its card shows only the facts of the moment.
/// </summary>
public static partial class CardTexts
{
    /// <summary>Two or three sentences: what fits under the HUD on a phone without scrolling. Checked by the tests.</summary>
    public const int MaxBodyLength = 400;

    internal const string ResourcePrefix = "cards/es/";
    private const char NoBreakSpace = ' ';

    private static readonly Lazy<IReadOnlyDictionary<CardKey, CardText>> Loaded = new(Load);

    public static IReadOnlyDictionary<CardKey, CardText> All => Loaded.Value;

    public static CardText? Find(CardKey key) => Loaded.Value.GetValueOrDefault(key);

    /// <summary>
    /// Reads a card file:
    /// <code>
    /// # Title
    ///
    /// Body, one or more paragraphs; lines within a paragraph are joined.
    ///
    /// ## Fuentes
    ///
    /// - One source per item.
    /// </code>
    /// Spaces inside numbers ("282 000") and between a number and its unit ("430 °C", "88 %", "109 m") become non-breaking,
    /// so a line never breaks there.
    /// </summary>
    public static CardText Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var lines = markdown.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd()).ToList();

        var start = lines.FindIndex(l => l.Length > 0);
        if (start < 0 || !lines[start].StartsWith("# ", StringComparison.Ordinal))
            throw new FormatException("A card starts with '# Title'.");
        var title = lines[start][2..].Trim();

        var paragraphs = new List<string>();
        var sources = new List<string>();
        var current = new StringBuilder();
        var inSources = false;
        foreach (var line in lines.Skip(start + 1))
        {
            if (line.StartsWith('#'))
            {
                if (line != "## Fuentes" || inSources)
                    throw new FormatException($"Unexpected heading '{line}': the only one allowed is '## Fuentes', once.");
                inSources = true;
                continue;
            }
            if (inSources)
            {
                if (line.StartsWith("- ", StringComparison.Ordinal))
                    sources.Add(line[2..].Trim());
                else if (line.Length > 0)
                    throw new FormatException($"Under '## Fuentes' every line is an item ('- …'), got '{line}'.");
                continue;
            }
            if (line.Length == 0)
            {
                Flush();
                continue;
            }
            if (current.Length > 0)
                current.Append(' ');
            current.Append(line.Trim());
        }
        Flush();

        if (title.Length == 0 || paragraphs.Count == 0)
            throw new FormatException("A card needs a title and a body.");
        if (sources.Count == 0)
            throw new FormatException("A card needs '## Fuentes' with at least one source.");
        return new CardText(title, KeepNumbersTogether(string.Join("\n\n", paragraphs)), sources);

        void Flush()
        {
            if (current.Length > 0)
                paragraphs.Add(current.ToString());
            current.Clear();
        }
    }

    /// <summary>File name of a card, from its key: "alf CMa" → "alf-CMa.md".</summary>
    public static string FileName(CardKey key) => key.Id.Replace(' ', '-') + ".md";

    private static string KeepNumbersTogether(string text) =>
        NumberSpace().Replace(text, NoBreakSpace.ToString());

    // A space after a digit and before three more digits ("282 000") or a unit ("430 °C", "88 %", "109 m", "40 km").
    [GeneratedRegex(@"(?<=\d) (?=\d{3}(?!\d)|%|°C|km\b|m\b)")]
    private static partial Regex NumberSpace();

    private static IReadOnlyDictionary<CardKey, CardText> Load()
    {
        var assembly = typeof(CardTexts).Assembly;
        var cards = new Dictionary<CardKey, CardText>();
        foreach (var name in assembly.GetManifestResourceNames())
        {
            // Logical names are built from the folder, whose separator depends on the build machine.
            var path = name.Replace('\\', '/');
            if (!path.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                continue;
            var relative = path[ResourcePrefix.Length..];
            // The dated entries of an object's history are read by CardHistory (decision 052).
            if (relative.StartsWith(CardHistory.Folder, StringComparison.Ordinal))
                continue;
            var key = KeyFromPath(relative);
            try
            {
                cards.Add(key, Parse(Read(assembly, name)));
            }
            catch (FormatException ex)
            {
                throw new FormatException($"Card '{path}': {ex.Message}", ex);
            }
        }
        return cards;
    }

    private static CardKey KeyFromPath(string path)
    {
        var parts = path.Split('/');
        if (parts.Length != 2 || !parts[1].EndsWith(".md", StringComparison.Ordinal))
            throw new FormatException($"Card '{path}' is not '<folder>/<id>.md'.");
        return new CardKey(KindOfFolder(parts[0]), parts[1][..^3].Replace('-', ' '));
    }

    /// <summary>The folder of each kind of card, here and under <see cref="CardHistory.Folder"/>.</summary>
    internal static CardKind KindOfFolder(string folder) => folder switch
    {
        "targets" => CardKind.Target,
        "stars" => CardKind.Star,
        "constellations" => CardKind.Constellation,
        _ => throw new FormatException($"Unknown card folder '{folder}'."),
    };

    internal static string Read(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
