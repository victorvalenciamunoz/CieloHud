using CieloHud.Core.Cards;

namespace CieloHud.Core.Tests.Stars;

/// <summary>The short list and the search of the STARS chip (decision 054).</summary>
public class StarChoicesTests
{
    private static readonly Observer Madrid = new(40.4168, -3.7038, 650);
    private static readonly DateTimeOffset OctoberEvening = new(2026, 10, 15, 21, 0, 0, TimeSpan.Zero);

    private readonly StarChoices _choices = new(new AstronomyEngineStarService(), new AstronomyEngineSunService());

    [Fact]
    public void Brightest_AreAtMostTenHighStars_InAlphabeticalOrder()
    {
        var list = _choices.Brightest(Madrid, OctoberEvening);

        Assert.Equal(StarChoices.MaxShown, list.Count);
        Assert.All(list, c => Assert.True(c.Position.AltitudeDegrees > StarChoices.MinAltitudeDegrees, $"{c.Name} at {c.Position.AltitudeDegrees:F1}°"));
        var keys = list.Select(c => StarChoices.Fold(c.Name)).ToList();
        Assert.Equal(keys.Order(StringComparer.Ordinal), keys);
        Assert.Contains(list, c => c.Name == "Vega" && c.Constellation == "la Lira");
    }

    /// <summary>Brightness picks which ones: every high star left out is fainter than every star in the list.</summary>
    [Fact]
    public void Brightest_LeaveOutOnlyFainterStars()
    {
        var service = new AstronomyEngineStarService();
        var list = _choices.Brightest(Madrid, OctoberEvening);
        var faintestIn = list.Max(c => c.Star.Magnitude);

        var leftOut = BrightStars.All
            .Where(s => list.All(c => c.Star != s))
            .Where(s => service.Locate(s, Madrid, OctoberEvening).AltitudeDegrees > StarChoices.MinAltitudeDegrees);

        Assert.All(leftOut, s => Assert.True(s.Magnitude >= faintestIn, $"{s.Name} ({s.Magnitude}) is brighter than one in the list"));
    }

    [Fact]
    public void Brightest_WithNothingHighEnough_IsEmpty()
    {
        var lowSky = new StarChoices(new FixedStars(altitude: 5), new FixedSun(-30));

        Assert.Empty(lowSky.Brightest(Madrid, OctoberEvening));
    }

    [Theory]
    [InlineData("vega", "alf Lyr")]
    [InlineData("VEGA", "alf Lyr")]
    [InlineData("betel", "alf Ori")]
    [InlineData("sirio", "alf CMa")]
    [InlineData("sirius", "alf CMa")]
    [InlineData("aldebaran", "alf Tau")] // without the accent of «Aldebarán»
    [InlineData("polar", "alf UMi")] // a later word of «Estrella Polar»
    [InlineData("casi", "gam Cas")] // «Gamma de Casiopea»
    [InlineData("gamma casiopea", "gam Cas")]
    [InlineData("épsilon perseo", "eps Per")] // every word of the query, with or without accents
    [InlineData("caroli", "alf02 CVn")]
    public void Matches_TheStartOfAnyWordOfItsNames(string query, string designation)
    {
        Assert.True(StarChoices.Matches(Designated(designation), query));
    }

    [Theory]
    [InlineData("", "alf Lyr")]
    [InlineData("   ", "alf Lyr")]
    [InlineData("ega", "alf Lyr")] // inside a word, not at its start
    [InlineData("vega x", "alf Lyr")] // a word that matches nothing
    [InlineData("lira", "alf Lyr")] // the constellation is not part of a proper name
    [InlineData("al", "alf Lyr")] // nor the designation: every «alf …» would match «al»
    [InlineData("alf lyr", "alf Lyr")]
    public void DoesNotMatch_OtherText(string query, string designation)
    {
        Assert.False(StarChoices.Matches(Designated(designation), query));
    }

    [Fact]
    public void Search_ShowsAtMostTen_AndSaysHowManyMatched()
    {
        var result = _choices.Search("al", Madrid, OctoberEvening);

        Assert.Equal(StarChoices.MaxShown, result.Shown.Count);
        Assert.True(result.HasMore);
        Assert.Equal(BrightStars.All.Count(s => StarChoices.Matches(s, "al")), result.Total);
        AssertStartingFirstThenAlphabetical(result.Shown, "al");
    }

    /// <summary>Seen on the phone: with "ve", Vega came after two stars «de la Vela», which only contain the word.</summary>
    [Fact]
    public void Search_PutsTheNamesThatStartWithTheQueryFirst()
    {
        var result = _choices.Search("ve", Madrid, OctoberEvening);

        Assert.Equal(["Vega", "Gamma 2 de la Vela", "Mu de la Vela"], result.Shown.Select(c => c.Name));
        AssertStartingFirstThenAlphabetical(result.Shown, "ve");
    }

    [Theory]
    [InlineData("ve", "alf Lyr", true)] // Vega
    [InlineData("VEGA", "alf Lyr", true)]
    [InlineData("sirius", "alf CMa", true)] // its IAU name
    [InlineData("estr", "alf UMi", true)] // «Estrella Polar»
    [InlineData("polar", "alf UMi", true)] // a later word of «Estrella Polar», but the start of «Polaris»
    [InlineData("caroli", "alf02 CVn", false)] // a later word of «Cor Caroli»: it matches, in the second group
    [InlineData("ve", "mu. Vel", false)] // «Mu de la Vela»
    [InlineData("casi", "gam Cas", false)] // «Gamma de Casiopea»
    [InlineData("", "alf Lyr", false)]
    public void StartsWith_TheFirstWordOfTheName(string query, string designation, bool starts)
    {
        Assert.Equal(starts, StarChoices.StartsWith(Designated(designation), query));
    }

    private static void AssertStartingFirstThenAlphabetical(IReadOnlyList<StarChoice> shown, string query)
    {
        var starting = shown.TakeWhile(c => StarChoices.StartsWith(c.Star, query)).ToList();
        var rest = shown.Skip(starting.Count).ToList();
        Assert.DoesNotContain(rest, c => StarChoices.StartsWith(c.Star, query));
        foreach (var group in new[] { starting, rest })
        {
            var keys = group.Select(c => StarChoices.Fold(c.Name)).ToList();
            Assert.Equal(keys.Order(StringComparer.Ordinal), keys);
        }
    }

    [Fact]
    public void Search_FindsStarsBelowTheHorizon_AndMarksThem()
    {
        // Sirius rises after midnight in October.
        var result = _choices.Search("sirio", Madrid, OctoberEvening);

        var sirius = Assert.Single(result.Shown);
        Assert.False(result.HasMore);
        Assert.False(sirius.AboveHorizon);
        Assert.Equal("el Can Mayor", sirius.Constellation);
    }

    [Fact]
    public void Search_BlankQuery_FindsNothing()
    {
        var result = _choices.Search(" ", Madrid, OctoberEvening);

        Assert.Empty(result.Shown);
        Assert.Equal(0, result.Total);
    }

    [Theory]
    [InlineData(-5.9, true)]
    [InlineData(-6.1, false)]
    [InlineData(30, true)]
    [InlineData(-40, false)]
    public void IsDaylight_WhileTheSunIsAboveCivilTwilight(double sunAltitude, bool daylight)
    {
        var choices = new StarChoices(new AstronomyEngineStarService(), new FixedSun(sunAltitude));

        Assert.Equal(daylight, choices.IsDaylight(Madrid, OctoberEvening));
    }

    [Fact]
    public void IsDaylight_AtNoonAndNotAtMidnight_InMadrid()
    {
        Assert.True(_choices.IsDaylight(Madrid, new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero)));
        Assert.False(_choices.IsDaylight(Madrid, new DateTimeOffset(2026, 10, 15, 23, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void EveryCatalogStar_HasTheConstellationOfItsDesignation()
    {
        foreach (var star in BrightStars.All)
            Assert.False(string.IsNullOrEmpty(StarChoices.ConstellationOf(star)), star.Designation);
        Assert.Equal("Orión", StarChoices.ConstellationOf(Designated("alf Ori")));
    }

    [Theory]
    [InlineData(47.4, 286.9, "en la Lira · 47° al O")]
    [InlineData(10.5, 328.7, "en la Lira · 11° al NO")]
    [InlineData(-16.8, 262.5, "en la Lira · bajo el horizonte")]
    public void Detail_SaysWhereToLook(double altitude, double azimuth, string detail)
    {
        var vega = Designated("alf Lyr");
        var choice = new StarChoice(vega, "Vega", "la Lira", new HorizontalPosition(azimuth, altitude));

        Assert.Equal(detail, StarChoices.Detail(choice));
    }

    [Theory]
    [InlineData("Épsilon del Cisne", "epsilon del cisne")]
    [InlineData("Estrella Polar", "estrella polar")]
    [InlineData("Gamma 2 de la Vela", "gamma 2 de la vela")]
    [InlineData("Al-Na'ir", "al na ir")]
    public void Fold_DropsAccentsCaseAndPunctuation(string text, string folded)
    {
        Assert.Equal(folded, StarChoices.Fold(text));
    }

    private static Star Designated(string designation) => BrightStars.All.Single(s => s.Designation == designation);

    private sealed class FixedSun(double altitude) : ISunService
    {
        public HorizontalPosition Locate(Observer observer, DateTimeOffset instant) => new(180, altitude);
    }

    private sealed class FixedStars(double altitude) : IStarService
    {
        public HorizontalPosition Locate(Star star, Observer observer, DateTimeOffset instant) => new(90, altitude);
    }
}
