using System.Text.RegularExpressions;
using CieloHud.Core.Constellations;

namespace CieloHud.Core.Tests.Cards;

public class CardTextsTests
{
    private const char NoBreakSpace = ' ';

    private const string Sample = """
        # Saturno

        El planeta de los anillos,
        hechos de hielo y roca.

        Segundo párrafo.

        ## Fuentes

        - NASA, Saturn Facts.
        - NASA, Planetary Fact Sheet.
        """;

    [Fact]
    public void Parse_ReadsTitleBodyAndSources()
    {
        var card = CardTexts.Parse(Sample);

        Assert.Equal("Saturno", card.Title);
        Assert.Equal("El planeta de los anillos, hechos de hielo y roca.\n\nSegundo párrafo.", card.Body);
        Assert.Equal(["NASA, Saturn Facts.", "NASA, Planetary Fact Sheet."], card.Sources);
    }

    [Fact]
    public void Parse_WindowsLineEndings_SameResult()
    {
        Assert.Equal(CardTexts.Parse(Sample).Body, CardTexts.Parse(Sample.Replace("\n", "\r\n")).Body);
    }

    [Theory]
    [InlineData("Hasta 282 000 km del planeta.", "Hasta 282 000 km del planeta.")]
    [InlineData("De 430 °C a −180 °C.", "De 430 °C a −180 °C.")]
    [InlineData("Mide 109 m de punta a punta, un 27 % más.", "Mide 109 m de punta a punta, un 27 % más.")]
    [InlineData("En 1610 mil Tierras", "En 1610 mil Tierras")] // a year is not a number group
    [InlineData("Cada 88 días, 10 metros.", "Cada 88 días, 10 metros.")] // units in words may break
    public void Parse_KeepsNumbersAndUnitsTogether(string body, string expected)
    {
        var card = CardTexts.Parse($"# T\n\n{body}\n\n## Fuentes\n\n- X\n");

        Assert.Equal(expected, card.Body);
    }

    [Theory]
    [InlineData("Sin título.\n\n## Fuentes\n\n- X")]
    [InlineData("# T\n\n## Fuentes\n\n- X")] // no body
    [InlineData("# T\n\nCuerpo.")] // no sources
    [InlineData("# T\n\nCuerpo.\n\n## Fuentes\n")] // empty sources
    [InlineData("# T\n\nCuerpo.\n\n## Otra\n\n## Fuentes\n\n- X")] // unknown heading
    [InlineData("# T\n\nCuerpo.\n\n## Fuentes\n\nTexto suelto")] // a source must be an item
    [InlineData("# T\n\nCuerpo.\n\n## Fuentes\n\n- X\n\n## Fuentes\n\n- Y")]
    public void Parse_MalformedCard_Throws(string markdown)
    {
        Assert.Throws<FormatException>(() => CardTexts.Parse(markdown));
    }

    [Theory]
    [InlineData("alf CMa", "alf-CMa.md")]
    [InlineData("gam02 Vel", "gam02-Vel.md")]
    [InlineData("Ori", "Ori.md")]
    public void FileName_FromTheKey(string id, string expected)
    {
        Assert.Equal(expected, CardTexts.FileName(new CardKey(CardKind.Star, id)));
    }

    [Fact]
    public void Targets_AllSevenHaveATextNamedAsInTheApp()
    {
        var expected = new Dictionary<CardKey, string>
        {
            [CardKey.Body(CelestialBody.Moon)] = "Luna",
            [CardKey.Body(CelestialBody.Mercury)] = "Mercurio",
            [CardKey.Body(CelestialBody.Venus)] = "Venus",
            [CardKey.Body(CelestialBody.Mars)] = "Marte",
            [CardKey.Body(CelestialBody.Jupiter)] = "Júpiter",
            [CardKey.Body(CelestialBody.Saturn)] = "Saturno",
            [CardKey.Iss] = "ISS",
        };

        foreach (var (key, title) in expected)
            Assert.Equal(title, CardTexts.Find(key)?.Title);
        Assert.Equal(expected.Count, CardTexts.All.Keys.Count(k => k.Kind == CardKind.Target));
    }

    [Fact]
    public void EveryText_IsForAKnownObject()
    {
        foreach (var key in CardTexts.All.Keys)
        {
            var known = key.Kind switch
            {
                CardKind.Target => key.Id == CardKey.IssId || Enum.TryParse<CelestialBody>(key.Id, out _),
                CardKind.Star => BrightStars.All.Any(s => s.Designation == key.Id),
                CardKind.Constellation => ConstellationFigures.Get(key.Id) is not null,
                _ => false,
            };
            Assert.True(known, $"No object for card {key}.");
        }
    }

    /// <summary>Written for the HUD: short, plain text, with sources, and nothing that changes with the date (that is computed).</summary>
    [Fact]
    public void EveryText_FollowsTheRules()
    {
        Assert.NotEmpty(CardTexts.All);
        foreach (var (key, card) in CardTexts.All)
        {
            Assert.True(card.Body.Length <= CardTexts.MaxBodyLength, $"{key}: {card.Body.Length} characters, max {CardTexts.MaxBodyLength}.");
            Assert.NotEmpty(card.Sources);
            Assert.DoesNotMatch(@"[*_`\[\]<>#]", card.Body); // shown as plain text
            Assert.DoesNotMatch(new Regex(@"\b(hoy|ahora|esta noche|este año|actualmente)\b", RegexOptions.IgnoreCase), card.Body);
            Assert.DoesNotContain("  ", card.Body);
        }
    }

    /// <summary>
    /// A star's card already computes how long its light took and explains its magnitude (decision 044): its text
    /// does not repeat them, so the two can never disagree (decision 046).
    /// </summary>
    [Fact]
    public void StarTexts_DoNotRepeatTheComputedFacts()
    {
        Assert.Contains(CardTexts.All.Keys, k => k.Kind == CardKind.Star);
        foreach (var (_, card) in CardTexts.All.Where(c => c.Key.Kind == CardKind.Star))
            Assert.DoesNotMatch(new Regex(@"años luz|magnitud", RegexOptions.IgnoreCase), card.Body.Replace(NoBreakSpace, ' '));
    }

    /// <summary>Texts still to write; update when a batch is committed. Their cards show only the facts.</summary>
    [Fact]
    public void PendingTexts()
    {
        Assert.Equal(95, BrightStars.All.Count(s => CardTexts.Find(CardKey.Star(s)) is null));
        Assert.Equal(88, ConstellationFigures.All.Count(c => CardTexts.Find(CardKey.Constellation(c.Symbol)) is null));
    }
}
