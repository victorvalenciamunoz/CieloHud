namespace CieloHud.Core.Tests.Cards;

public class FactsTextTests
{
    private const char Space = ' ';
    private static readonly TimeZoneInfo Madrid = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");
    private static readonly MoonQuarter AnyQuarter = new(MoonQuarterKind.FullMoon, new DateTimeOffset(2026, 10, 26, 4, 12, 0, TimeSpan.Zero));

    private static MoonFacts Moon(double fraction = 0.5, MoonPhaseName phase = MoonPhaseName.FirstQuarter, double distanceKm = 384_400) =>
        new(distanceKm, LightTravel.Time(distanceKm), fraction, 90, phase, AnyQuarter);

    [Theory]
    [InlineData(0.7077, MoonPhaseName.WaxingGibbous, "Gibosa creciente, iluminada al 71")]
    [InlineData(0.215, MoonPhaseName.WaningCrescent, "Luna menguante, iluminada al 22")] // halves up
    [InlineData(0.002, MoonPhaseName.NewMoon, "Luna nueva, iluminada al 0")]
    [InlineData(0.9996, MoonPhaseName.FullMoon, "Luna llena, iluminada al 100")]
    [InlineData(0.5, MoonPhaseName.LastQuarter, "Cuarto menguante, iluminada al 50")]
    public void MoonPhase_NamesThePhaseAndTheLitPercentage(double fraction, MoonPhaseName phase, string expectedBeforePercent)
    {
        // The percent sign stays with its number: non-breaking space.
        Assert.Equal($"{expectedBeforePercent}{Space}%", FactsText.MoonPhase(Moon(fraction, phase)));
    }

    [Fact]
    public void PhaseName_EveryPhaseHasASpanishName()
    {
        var names = Enum.GetValues<MoonPhaseName>().Select(FactsText.PhaseName).ToList();

        Assert.Equal(8, names.Distinct().Count());
        Assert.Equal(
            ["Luna nueva", "Luna creciente", "Cuarto creciente", "Gibosa creciente", "Luna llena", "Gibosa menguante", "Cuarto menguante", "Luna menguante"],
            names);
    }

    [Theory]
    [InlineData("2026-10-26T04:12:00Z", "2026-10-25T20:00:00Z", "Luna llena mañana a las 5:12")] // CET after the 25 Oct change
    [InlineData("2026-10-26T04:12:00Z", "2026-10-26T01:00:00Z", "Luna llena hoy a las 5:12")]
    [InlineData("2026-10-26T04:12:00Z", "2026-10-20T20:00:00Z", "Luna llena el lun 26 oct a las 5:12")]
    [InlineData("2026-11-09T00:05:00Z", "2026-11-07T20:00:00Z", "Luna llena el lun 9 nov a la 1:05")]
    public void NextQuarter_SaysTheDayAndLocalTime(string at, string now, string expected)
    {
        var quarter = new MoonQuarter(MoonQuarterKind.FullMoon, DateTimeOffset.Parse(at));

        Assert.Equal(expected, FactsText.NextQuarter(quarter, DateTimeOffset.Parse(now), Madrid));
    }

    [Theory]
    [InlineData(MoonQuarterKind.NewMoon, "Luna nueva")]
    [InlineData(MoonQuarterKind.FirstQuarter, "Cuarto creciente")]
    [InlineData(MoonQuarterKind.LastQuarter, "Cuarto menguante")]
    public void NextQuarter_NamesEachPrincipalPhase(MoonQuarterKind kind, string expectedStart)
    {
        var quarter = new MoonQuarter(kind, new DateTimeOffset(2026, 10, 10, 15, 50, 0, TimeSpan.Zero));

        Assert.StartsWith(expectedStart + " ", FactsText.NextQuarter(quarter, quarter.Instant.AddDays(-3), Madrid));
    }

    [Fact]
    public void MoonDistance_ToTheHundredKilometersWithItsLightTime()
    {
        Assert.Equal($"A 369{Space}600{Space}km: su luz tarda 1,2 segundos en llegar", FactsText.MoonDistance(Moon(distanceKm: 369_645)));
        Assert.Equal($"A 405{Space}500{Space}km: su luz tarda 1,4 segundos en llegar", FactsText.MoonDistance(Moon(distanceKm: 405_450)));
    }

    [Theory]
    [InlineData(876_400_000, "A 876 millones de km")]
    [InlineData(47_985_000, "A 48 millones de km")]
    [InlineData(1_261_800_000, "A 1262 millones de km")]
    public void PlanetDistance_ToTheMillion(double km, string expected)
    {
        var planet = new PlanetFacts(CelestialBody.Jupiter, km, LightTravel.Time(km));

        Assert.Equal(expected.Replace("de km", $"de{Space}km"), FactsText.PlanetDistance(planet));
    }

    [Fact]
    public void PlanetLight_SaysHowLongAgoTheLightLeftThePlanet()
    {
        var jupiter = new PlanetFacts(CelestialBody.Jupiter, 876_400_000, TimeSpan.FromMinutes(48.72));

        Assert.Equal("Esta luz salió de Júpiter hace 49 minutos", FactsText.PlanetLight(jupiter));
    }

    [Theory]
    [InlineData(1.23, "1 segundo")]
    [InlineData(45, "45 segundos")]
    [InlineData(160.05, "2 min y 40 s")] // Venus
    [InlineData(180.2, "3 minutos")]
    [InlineData(60, "1 minuto")]
    [InlineData(599.4, "9 min y 59 s")]
    [InlineData(599.6, "10 minutos")]
    [InlineData(2923.3, "49 minutos")] // Jupiter
    [InlineData(3569, "59 minutos")]
    [InlineData(3571, "1 hora")]
    [InlineData(4208.9, "1 h y 10 min")] // Saturn
    [InlineData(7200, "2 horas")]
    public void Duration_InTheWordsPeopleUse(double seconds, string expected)
    {
        Assert.Equal(expected, FactsText.Duration(TimeSpan.FromSeconds(seconds)));
    }

    private static GalileanMoon Moon(GalileanMoonName name, double right, double up, GalileanMoonState state = GalileanMoonState.Visible) =>
        new(name, right, up, 0, state);

    private static JupiterMoonsFacts Jupiter(params GalileanMoon[] moons) => new(moons, 17);

    [Fact]
    public void JupiterMoons_FlatRow_LeftToRightWithJupiterInItsPlace()
    {
        var lines = FactsText.JupiterMoons(Jupiter(
            Moon(GalileanMoonName.Io, -5, 0.5),
            Moon(GalileanMoonName.Europa, 8, -1),
            Moon(GalileanMoonName.Ganymede, 14, -2),
            Moon(GalileanMoonName.Callisto, -24, 3)));

        Assert.Equal(["Con prismáticos, de izquierda a derecha: Calisto, Ío, Júpiter, Europa y Ganímedes"], lines);
    }

    [Fact]
    public void JupiterMoons_UprightRow_TopToBottom()
    {
        // Jupiter rising in the east, 2026-10-08 06:00 local time from Madrid.
        var lines = FactsText.JupiterMoons(Jupiter(
            Moon(GalileanMoonName.Io, 1.2, 4.0),
            Moon(GalileanMoonName.Europa, -2.8, -8.8),
            Moon(GalileanMoonName.Ganymede, 4.3, 13.9),
            Moon(GalileanMoonName.Callisto, 6.1, 19.5)));

        Assert.Equal(["Con prismáticos, de arriba abajo: Calisto, Ganímedes, Ío, Júpiter y Europa"], lines);
    }

    [Fact]
    public void JupiterMoons_HiddenMoons_SaidApart()
    {
        var lines = FactsText.JupiterMoons(Jupiter(
            Moon(GalileanMoonName.Io, 0.4, 0.1, GalileanMoonState.BehindJupiter),
            Moon(GalileanMoonName.Europa, -0.5, 0, GalileanMoonState.InFrontOfJupiter),
            Moon(GalileanMoonName.Ganymede, 3, 0.5, GalileanMoonState.InJupitersShadow),
            Moon(GalileanMoonName.Callisto, -20, -2)));

        Assert.Equal(
            [
                "Con prismáticos, de izquierda a derecha: Calisto y Júpiter",
                "Ío está detrás de Júpiter",
                "Europa pasa por delante de Júpiter",
                "Ganímedes está en la sombra de Júpiter",
            ],
            lines);
    }

    [Fact]
    public void JupiterMoons_NoneVisible()
    {
        var lines = FactsText.JupiterMoons(Jupiter(Moon(GalileanMoonName.Io, 0.1, 0, GalileanMoonState.BehindJupiter)));

        Assert.Equal(["Ahora no se ve ninguna de sus cuatro lunas grandes", "Ío está detrás de Júpiter"], lines);
    }

    private static readonly DateTimeOffset Today = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SaturnRings_TiltedAndOpening()
    {
        var rings = new SaturnRingsFacts(-7.36, RingTrend.Opening, new DateTimeOffset(2032, 4, 12, 9, 0, 0, TimeSpan.Zero), 26.74);

        Assert.Equal(
            ["Con un telescopio pequeño, sus anillos se ven inclinados 7°", "Se irán abriendo hasta 2032, cuando llegarán a 27°"],
            FactsText.SaturnRings(rings, Today));
    }

    [Fact]
    public void SaturnRings_Closing_EdgeOnSoon_SaysTheMonth()
    {
        var rings = new SaturnRingsFacts(1.31, RingTrend.Closing, new DateTimeOffset(2027, 5, 6, 0, 0, 0, TimeSpan.Zero), 0);

        Assert.Equal(
            ["Con un telescopio pequeño, sus anillos se ven casi de canto, como una raya fina", "Se irán cerrando hasta mayo de 2027, cuando se verán de canto"],
            FactsText.SaturnRings(rings, Today));
    }

    [Theory]
    [InlineData(-1.99, "casi de canto")]
    [InlineData(2.0, "inclinados 2°")]
    [InlineData(26.5, "inclinados 27°")] // halves up
    public void SaturnRings_TiltWithoutSign(double tilt, string expected)
    {
        var rings = new SaturnRingsFacts(tilt, RingTrend.Closing, Today.AddYears(5), 0);

        Assert.Contains(expected, FactsText.SaturnRings(rings, Today)[0]);
    }

    [Fact]
    public void Duration_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FactsText.Duration(TimeSpan.FromSeconds(-1)));
    }

    private static SatelliteFacts Iss(
        double altitudeKm = 424.07, double distanceKm = 567.4, double speed = 7.6622, SatelliteSight sight = SatelliteSight.Visible) =>
        new(altitudeKm, distanceKm, speed, TimeSpan.FromMinutes(92.99), sight);

    [Theory]
    [InlineData(424.07, 567.4, "A 424 km de altura y a 570 km de ti")]
    [InlineData(418.6, 1234.0, "A 419 km de altura y a 1230 km de ti")]
    [InlineData(421.0, 2363.5, "A 421 km de altura y a 2360 km de ti")]
    public void SatelliteAltitude_HeightToTheKilometerDistanceToTen(double altitudeKm, double distanceKm, string expected)
    {
        Assert.Equal(expected.Replace(" km", $"{Space}km"), FactsText.SatelliteAltitude(Iss(altitudeKm, distanceKm)));
    }

    [Fact]
    public void SatelliteSpeed_KmPerHourToTheHundredAndKmPerSecond()
    {
        // 7.6622 km/s = 27 584 km/h.
        Assert.Equal($"Va a 27{Space}600{Space}km/h, 7,7{Space}km cada segundo", FactsText.SatelliteSpeed(Iss()));
    }

    [Theory]
    [InlineData(SatelliteSight.Visible, "La ilumina el Sol y tu cielo está oscuro: se puede ver a simple vista")]
    [InlineData(SatelliteSight.SkyTooBright, "La ilumina el Sol, pero hay demasiada luz en el cielo para verla")]
    [InlineData(SatelliteSight.InEarthShadow, "Está en la sombra de la Tierra: ahora no se ve")]
    [InlineData(SatelliteSight.BelowHorizon, "Ya está bajo el horizonte: ahora no se ve")]
    public void SatelliteVisibility_SaysWhyItShowsOrNot(SatelliteSight sight, string expected)
    {
        Assert.Equal(expected, FactsText.SatelliteVisibility(Iss(sight: sight)));
    }

    [Fact]
    public void JupiterMoons_EBeforeIo()
    {
        var lines = FactsText.JupiterMoons(Jupiter(
            Moon(GalileanMoonName.Io, 5, 0),
            Moon(GalileanMoonName.Europa, -0.5, 0, GalileanMoonState.InFrontOfJupiter)));
        Assert.Equal("Con prismáticos, de izquierda a derecha: Júpiter e Ío", lines[0]);
    }

    private static ConstellationFacts Constellation(double fraction = 0.0144, int rank = 26, IReadOnlyList<Core.Stars.Star>? stars = null) =>
        new("Ori", fraction, rank, ConstellationSight.Whole, stars);

    [Theory]
    [InlineData(0.01440, 26, "Ocupa el 1,4 % del cielo: la 26.ª de 88 por tamaño")]
    [InlineData(0.03158, 1, "Ocupa el 3,2 % del cielo: la más grande de las 88")]
    [InlineData(0.00166, 88, "Ocupa el 0,2 % del cielo: la más pequeña de las 88")]
    [InlineData(0.02571, 9, "Ocupa el 2,6 % del cielo: la 9.ª de 88 por tamaño")]
    public void ConstellationSize_ShareOfTheSkyAndPlace(double fraction, int rank, string expected)
    {
        Assert.Equal(expected.Replace(" %", $"{Space}%"), FactsText.ConstellationSize(Constellation(fraction, rank)));
    }

    [Theory]
    [InlineData(ConstellationSight.Whole, "Desde aquí se puede ver entera")]
    [InlineData(ConstellationSight.Partly, "Desde aquí solo se ve una parte: el resto no llega a salir")]
    [InlineData(ConstellationSight.NeverRises, "Desde aquí no sale nunca")]
    [InlineData(ConstellationSight.NeverSets, "Desde aquí no se pone nunca")]
    public void ConstellationVisibility_FromTheObserversLatitude(ConstellationSight sight, string expected)
    {
        Assert.Equal(expected, FactsText.ConstellationVisibility(sight));
    }

    [Fact]
    public void ConstellationBrightest_UpToThree()
    {
        Assert.Equal("Ninguna de sus estrellas llega a la magnitud 3: desde ciudad cuesta verla", FactsText.ConstellationBrightest([]));
        Assert.Equal("Su estrella más brillante: Vega", FactsText.ConstellationBrightest(["Vega"]));
        Assert.Equal("Sus estrellas más brillantes: Alphard y Gamma de la Hidra", FactsText.ConstellationBrightest(["Alphard", "Gamma de la Hidra"]));
        Assert.Equal("Sus estrellas más brillantes: Arturo e Izar", FactsText.ConstellationBrightest(["Arturo", "Izar"]));
        Assert.Equal("Sus estrellas más brillantes: Rigel, Betelgeuse y Bellatrix",
            FactsText.ConstellationBrightest(["Rigel", "Betelgeuse", "Bellatrix", "Alnilam", "Alnitak"]));
    }

    [Fact]
    public void Constellation_StarsLineOnlyWhenTheCatalogCanTell()
    {
        var rigel = Core.Stars.BrightStars.Get("Rigel");
        Assert.Equal(3, FactsText.Constellation(Constellation(stars: [rigel]), ["Rigel"]).Count);
        Assert.Equal(2, FactsText.Constellation(Constellation(stars: null), null).Count);
        Assert.Throws<ArgumentException>(() => FactsText.Constellation(Constellation(stars: [rigel]), null));
        Assert.Throws<ArgumentException>(() => FactsText.Constellation(Constellation(stars: [rigel]), ["Rigel", "Saiph"]));
    }

    [Theory]
    [InlineData(1959, 10, 7, "7 oct 1959")]
    [InlineData(1054, 7, 4, "4 jul 1054")]
    [InlineData(2000, 11, 2, "2 nov 2000")]
    [InlineData(1610, 1, 17, "17 ene 1610")]
    public void HistoryDate_DayMonthAndYear_OnOneLine(int year, int month, int day, string expected)
    {
        Assert.Equal(expected.Replace(' ', Space), FactsText.HistoryDate(new DateOnly(year, month, day)));
    }
}
