using System.Text.RegularExpressions;

namespace CieloHud.Core.Tests.Cards;

public class CardHistoryTests
{
    [Theory]
    [InlineData("targets/1610-01-07-Jupiter.md", 1610, 1, 7, CardKind.Target, "Jupiter")]
    [InlineData("targets/2000-11-02-ISS.md", 2000, 11, 2, CardKind.Target, "ISS")]
    [InlineData("stars/1862-01-31-alf-CMa.md", 1862, 1, 31, CardKind.Star, "alf CMa")]
    [InlineData("constellations/1604-10-09-Oph.md", 1604, 10, 9, CardKind.Constellation, "Oph")]
    public void FromPath_ReadsTheDateAndTheObject(string path, int year, int month, int day, CardKind kind, string id)
    {
        var (date, card) = CardHistory.FromPath(path);

        Assert.Equal(new DateOnly(year, month, day), date);
        Assert.Equal(new CardKey(kind, id), card);
    }

    [Theory]
    [InlineData("1610-01-07-Jupiter.md")] // no folder
    [InlineData("planets/1610-01-07-Jupiter.md")] // unknown folder
    [InlineData("targets/Jupiter.md")] // no date
    [InlineData("targets/1610-13-07-Jupiter.md")] // no such month
    [InlineData("targets/1610-02-30-Jupiter.md")] // no such day
    [InlineData("targets/1610-01-07Jupiter.md")] // no dash after the date
    [InlineData("targets/1610-01-07-.md")] // no object
    [InlineData("targets/1610-01-07-Jupiter.txt")]
    public void FromPath_Malformed_Throws(string path)
    {
        Assert.Throws<FormatException>(() => CardHistory.FromPath(path));
    }

    [Fact]
    public void TheFirstEntries_AreThere()
    {
        Assert.Contains(CardHistory.For(CardKey.Body(CelestialBody.Moon)), e => e.Date == new DateOnly(1959, 10, 7));
        Assert.Contains(CardHistory.For(CardKey.Body(CelestialBody.Jupiter)), e => e.Date == new DateOnly(1610, 1, 7));
        Assert.Contains(CardHistory.For(CardKey.Constellation("Oph")), e => e.Date == new DateOnly(1604, 10, 9));
    }

    [Fact]
    public void For_AnObjectWithoutHistory_IsEmpty()
    {
        Assert.Empty(CardHistory.For(new CardKey(CardKind.Star, "zzz Zzz")));
    }

    [Fact]
    public void For_ListsTheEntriesOldestFirst()
    {
        foreach (var key in CardHistory.All.Select(e => e.Card).Distinct())
        {
            var dates = CardHistory.For(key).Select(e => e.Date).ToList();
            Assert.Equal(dates.Order(), dates);
        }
    }

    [Fact]
    public void HistoryEntries_AreNotCardTexts()
    {
        Assert.DoesNotContain(CardTexts.All.Keys, k => char.IsDigit(k.Id[0]));
    }

    [Fact]
    public void EveryEntry_IsForAKnownObject()
    {
        foreach (var entry in CardHistory.All)
            Assert.True(KnownCards.Exists(entry.Card), $"No object for history entry {entry.Card} {entry.Date}.");
    }

    [Fact]
    public void AnObject_HasAtMostFourEntries()
    {
        foreach (var group in CardHistory.All.GroupBy(e => e.Card))
            Assert.True(group.Count() <= CardHistory.MaxPerObject, $"{group.Key}: {group.Count()} entries, max {CardHistory.MaxPerObject}.");
    }

    /// <summary>
    /// Short and plain like the card texts, and nothing that depends on today's date: the card lists the date, and how long ago
    /// it was would go stale (decision 051).
    /// </summary>
    [Fact]
    public void EveryEntry_FollowsTheRules()
    {
        Assert.NotEmpty(CardHistory.All);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var stale = new Regex(@"\b(hoy|ahora|esta noche|este año|actualmente|aniversario|tal día como)\b|\bse cumpl|\bhace \d", RegexOptions.IgnoreCase);
        foreach (var entry in CardHistory.All)
        {
            var name = $"{entry.Card} {entry.Date:yyyy-MM-dd}";
            var body = entry.Text.Body;
            Assert.True(body.Length <= CardHistory.MaxBodyLength, $"{name}: {body.Length} characters, max {CardHistory.MaxBodyLength}.");
            Assert.True(entry.Date <= today, $"{name}: a history entry has already happened.");
            Assert.DoesNotMatch(@"[*_`\[\]<>#]", body);
            Assert.DoesNotMatch(stale, body);
            Assert.DoesNotContain("  ", body);
            Assert.DoesNotContain('\n', body); // one paragraph: a line of the list
        }
    }

    /// <summary>Every date can be checked: a source gives its year, and before the Gregorian calendar, says it is Julian.</summary>
    [Fact]
    public void EveryDate_IsInItsSources()
    {
        foreach (var entry in CardHistory.All)
        {
            var name = $"{entry.Card} {entry.Date:yyyy-MM-dd}";
            Assert.True(entry.Text.Sources.Any(s => s.Contains(entry.Date.Year.ToString(), StringComparison.Ordinal)),
                $"{name}: no source gives the year.");
            if (entry.Date < CardHistory.GregorianStart)
                Assert.True(entry.Text.Sources.Any(s => s.Contains("juliano", StringComparison.OrdinalIgnoreCase)),
                    $"{name}: a date before 1582 must say in its sources that it is Julian.");
        }
    }
}
