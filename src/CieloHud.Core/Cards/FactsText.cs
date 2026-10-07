using CieloHud.Core.Alerts;
using CieloHud.Core.Events;
using static CieloHud.Core.Alerts.AlertWords;

namespace CieloHud.Core.Cards;

/// <summary>
/// The facts of the moment in Spanish, as the cards show them: "Gibosa creciente, iluminada al 71 %",
/// "Esta luz salió de Júpiter hace 49 minutos". Numbers are rounded to what the eye or the mind can use.
/// Thousands and units are separated by a non-breaking space ("369 600 km", "71 %"), as Spanish writes them.
/// </summary>
public static class FactsText
{
    private const char Space = ' ';

    public static string PhaseName(MoonPhaseName phase) => phase switch
    {
        MoonPhaseName.NewMoon => "Luna nueva",
        MoonPhaseName.WaxingCrescent => "Luna creciente",
        MoonPhaseName.FirstQuarter => "Cuarto creciente",
        MoonPhaseName.WaxingGibbous => "Gibosa creciente",
        MoonPhaseName.FullMoon => "Luna llena",
        MoonPhaseName.WaningGibbous => "Gibosa menguante",
        MoonPhaseName.LastQuarter => "Cuarto menguante",
        MoonPhaseName.WaningCrescent => "Luna menguante",
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };

    public static string QuarterName(MoonQuarterKind kind) => kind switch
    {
        MoonQuarterKind.NewMoon => "Luna nueva",
        MoonQuarterKind.FirstQuarter => "Cuarto creciente",
        MoonQuarterKind.FullMoon => "Luna llena",
        MoonQuarterKind.LastQuarter => "Cuarto menguante",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>"Gibosa creciente, iluminada al 71 %".</summary>
    public static string MoonPhase(MoonFacts moon) =>
        $"{PhaseName(moon.Phase)}, iluminada al {Round(moon.IlluminatedFraction * 100).ToString(Culture)}{Space}%";

    /// <summary>"Luna llena hoy a las 5:12", "… mañana a la 1:05", "… el lun 26 oct a las 5:12", in the observer's time zone.</summary>
    public static string NextQuarter(MoonQuarter quarter, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var day = UpcomingEvents.DayLabel(quarter.Instant, now, timeZone);
        var when = day is "Hoy" or "Mañana" ? day.ToLowerInvariant() : $"el {day}";
        return $"{QuarterName(quarter.Kind)} {when} a {TheTime(TimeZoneInfo.ConvertTime(quarter.Instant, timeZone))}";
    }

    /// <summary>"A 369 600 km: su luz tarda 1,2 segundos en llegar". Distance to the hundred kilometers.</summary>
    public static string MoonDistance(MoonFacts moon)
    {
        var km = Round(moon.DistanceKm / 100) * 100;
        var seconds = moon.LightTime.TotalSeconds.ToString("0.0", Culture).Replace('.', ',');
        return $"A {Grouped(km)}{Space}km: su luz tarda {seconds} segundos en llegar";
    }

    /// <summary>"A 876 millones de km", to the million.</summary>
    public static string PlanetDistance(PlanetFacts planet) =>
        $"A {Round(planet.DistanceKm / 1e6).ToString(Culture)} millones de{Space}km";

    /// <summary>"Esta luz salió de Júpiter hace 49 minutos".</summary>
    public static string PlanetLight(PlanetFacts planet) =>
        $"Esta luz salió de {ConjunctionAlertText.PlanetName(planet.Body)} hace {Duration(planet.LightTime)}";

    public static string MoonName(GalileanMoonName moon) => moon switch
    {
        GalileanMoonName.Io => "Ío",
        GalileanMoonName.Europa => "Europa",
        GalileanMoonName.Ganymede => "Ganímedes",
        GalileanMoonName.Callisto => "Calisto",
        _ => throw new ArgumentOutOfRangeException(nameof(moon), moon, null),
    };

    /// <summary>
    /// Jupiter's moons as binoculars show them (NASA: "Most binoculars will show at least one or two moons"): the visible ones
    /// and Jupiter in a row, "de izquierda a derecha", or "de arriba abajo" when the row stands more upright than flat (Jupiter
    /// low in the east or west); then a line for each moon that cannot be seen: "Ío está detrás de Júpiter".
    /// </summary>
    public static IReadOnlyList<string> JupiterMoons(JupiterMoonsFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var flat = facts.Moons.Sum(m => Math.Abs(m.RightRadii)) >= facts.Moons.Sum(m => Math.Abs(m.UpRadii));
        var visible = facts.Moons.Where(m => m.State == GalileanMoonState.Visible).ToList();

        var lines = new List<string>();
        if (visible.Count == 0)
        {
            lines.Add("Ahora no se ve ninguna de sus cuatro lunas grandes");
        }
        else
        {
            var row = visible
                .Select(m => (Name: MoonName(m.Name), Position: flat ? m.RightRadii : -m.UpRadii))
                .Append((Name: "Júpiter", Position: 0.0))
                .OrderBy(x => x.Position)
                .Select(x => x.Name)
                .ToList();
            lines.Add($"Con prismáticos, {(flat ? "de izquierda a derecha" : "de arriba abajo")}: {List(row)}");
        }

        foreach (var moon in facts.Moons.Where(m => m.State != GalileanMoonState.Visible))
        {
            lines.Add(moon.State switch
            {
                GalileanMoonState.BehindJupiter => $"{MoonName(moon.Name)} está detrás de Júpiter",
                GalileanMoonState.InFrontOfJupiter => $"{MoonName(moon.Name)} pasa por delante de Júpiter",
                GalileanMoonState.InJupitersShadow => $"{MoonName(moon.Name)} está en la sombra de Júpiter",
                _ => throw new ArgumentOutOfRangeException(nameof(facts), moon.State, null),
            });
        }
        return lines;
    }

    // Below this the rings are a thin line even in a telescope.
    private const double EdgeOnDegrees = 2;

    private static readonly string[] MonthNames =
        ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"];

    /// <summary>
    /// Saturn's rings as a small telescope shows them (NASA: "Even a small telescope will allow you to see more details of
    /// Saturn's rings"; binoculars only hint at them): "inclinados 7°", or "casi de canto" under 2°; then where they are heading,
    /// "Se irán abriendo hasta 2032, cuando llegarán a 27°" (the month too when it is less than a year away).
    /// </summary>
    public static IReadOnlyList<string> SaturnRings(SaturnRingsFacts rings, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(rings);
        var tilt = Math.Abs(rings.TiltDegrees);
        var seen = tilt < EdgeOnDegrees
            ? "Con un telescopio pequeño, sus anillos se ven casi de canto, como una raya fina"
            : $"Con un telescopio pequeño, sus anillos se ven inclinados {Degrees(tilt)}°";
        var until = rings.Until - now < TimeSpan.FromDays(365)
            ? $"{MonthNames[rings.Until.Month - 1]} de {rings.Until.Year.ToString(Culture)}"
            : rings.Until.Year.ToString(Culture);
        var trend = rings.Trend == RingTrend.Opening
            ? $"Se irán abriendo hasta {until}, cuando llegarán a {Degrees(rings.TiltAtUntilDegrees)}°"
            : $"Se irán cerrando hasta {until}, cuando se verán de canto";
        return [seen, trend];
    }

    /// <summary>"A 424 km de altura y a 650 km de ti": the height to the kilometer, the distance to ten.</summary>
    public static string SatelliteAltitude(SatelliteFacts satellite) =>
        $"A {Grouped(Round(satellite.AltitudeKm))}{Space}km de altura y a {Grouped(Round(satellite.DistanceKm / 10) * 10)}{Space}km de ti";

    /// <summary>
    /// "Va a 27 600 km/h, 7,7 km cada segundo": to the hundred km/h, and to the tenth per second. Not the turn around the Earth:
    /// the written text already says it (every hour and a half), and it hardly changes.
    /// </summary>
    public static string SatelliteSpeed(SatelliteFacts satellite) =>
        $"Va a {Grouped(Round(satellite.SpeedKmPerSecond * 3600 / 100) * 100)}{Space}km/h, " +
        $"{satellite.SpeedKmPerSecond.ToString("0.0", Culture).Replace('.', ',')}{Space}km cada segundo";

    /// <summary>Why it shows or not right now: what lights it and how dark the sky is.</summary>
    public static string SatelliteVisibility(SatelliteFacts satellite) => satellite.Sight switch
    {
        SatelliteSight.Visible => "La ilumina el Sol y tu cielo está oscuro: se puede ver a simple vista",
        SatelliteSight.SkyTooBright => "La ilumina el Sol, pero hay demasiada luz en el cielo para verla",
        SatelliteSight.InEarthShadow => "Está en la sombra de la Tierra: ahora no se ve",
        SatelliteSight.BelowHorizon => "Ya está bajo el horizonte: ahora no se ve",
        _ => throw new ArgumentOutOfRangeException(nameof(satellite), satellite.Sight, null),
    };

    /// <summary>
    /// "Esta luz salió de Sirio hace 8,6 años", "… hace unos 500 años", "… hace entre 1600 y 2700 años", "… hace más de 1100 años":
    /// two significant figures, worded by how sure the parallax is (decision 044). "Más de" rounds down, so it stays true.
    /// </summary>
    /// <param name="name">The star's name as the app shows it ("Sirio", "Gamma de Casiopea").</param>
    public static string StarLight(string name, StarLight light)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var when = light.Certainty switch
        {
            DistanceCertainty.Precise => $"hace {Years(TwoFigures(light.Years))}",
            DistanceCertainty.About => $"hace unos {Years(TwoFigures(light.Years))}",
            DistanceCertainty.Between => $"hace entre {Figures(TwoFigures(light.NearYears))} y {Years(TwoFigures(light.FarYears))}",
            DistanceCertainty.MoreThan => $"hace más de {Years(TwoFigures(light.NearYears, roundDown: true))}",
            _ => throw new ArgumentOutOfRangeException(nameof(light), light.Certainty, null),
        };
        return $"Esta luz salió de {name} {when}";

        static string Years(double years) => $"{Figures(years)} {(years == 1 ? "año" : "años")}";
    }

    /// <summary>"Su luz es anaranjada".</summary>
    public static string StarColorName(StarColor color) => color switch
    {
        Cards.StarColor.Bluish => "Su luz es azulada",
        Cards.StarColor.White => "Su luz es blanca",
        Cards.StarColor.YellowishWhite => "Su luz es de un blanco amarillento",
        Cards.StarColor.Yellowish => "Su luz es amarillenta",
        Cards.StarColor.Orange => "Su luz es anaranjada",
        Cards.StarColor.Reddish => "Su luz es rojiza",
        _ => throw new ArgumentOutOfRangeException(nameof(color), color, null),
    };

    /// <summary>
    /// "Brillo: magnitud 0,4". Just the number: the explanation of the backwards scale ("cuanto menor, más brilla…") was
    /// dropped after seeing the cards with their texts (decision 046).
    /// </summary>
    public static string StarMagnitude(double magnitude)
    {
        var rounded = Math.Round(magnitude, 1, MidpointRounding.AwayFromZero);
        var number = Math.Abs(rounded).ToString("0.0", Culture).Replace('.', ',');
        return $"Brillo: magnitud {(rounded < 0 ? "−" : "")}{number}";
    }

    /// <summary>8.64 → 8.6, 432.6 → 430, 1157 → 1200 (or 1100 rounding down).</summary>
    private static double TwoFigures(double x, bool roundDown = false)
    {
        var scale = Math.Pow(10, Math.Floor(Math.Log10(x)) - 1);
        return (roundDown ? Math.Floor(x / scale) : Math.Round(x / scale, MidpointRounding.AwayFromZero)) * scale;
    }

    /// <summary>"8,6" with a decimal comma below 10, whole numbers grouped above ("13 000").</summary>
    private static string Figures(double x) =>
        x < 10 ? x.ToString("0.#", Culture).Replace('.', ',') : Grouped((int)Math.Round(x));

    /// <summary>"A", "A y B", "A, B y C".</summary>
    private static string List(IReadOnlyList<string> items) =>
        items.Count == 1 ? items[0] : $"{string.Join(", ", items.Take(items.Count - 1))} y {items[^1]}";

    /// <summary>
    /// A light travel time in the words people use: "2 min y 40 s" under 10 minutes (Venus, Mercury, the Sun), whole minutes
    /// up to the hour ("49 minutos"), then hours and minutes ("1 h y 10 min", "2 horas").
    /// </summary>
    public static string Duration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Duration cannot be negative.");

        var totalSeconds = Round(duration.TotalSeconds);
        if (totalSeconds < 10 * 60)
        {
            var (minutes, seconds) = (totalSeconds / 60, totalSeconds % 60);
            if (minutes == 0)
                return Plural(seconds, "segundo", "segundos");
            return seconds == 0 ? Plural(minutes, "minuto", "minutos") : $"{minutes} min y {seconds} s";
        }

        var totalMinutes = Round(duration.TotalMinutes);
        if (totalMinutes < 60)
            return Plural(totalMinutes, "minuto", "minutos");
        var (hours, rest) = (totalMinutes / 60, totalMinutes % 60);
        return rest == 0 ? Plural(hours, "hora", "horas") : $"{hours} h y {rest} min";
    }

    private static string Plural(int n, string one, string many) => $"{n.ToString(Culture)} {(n == 1 ? one : many)}";

    /// <summary>369600 → "369 600" (non-breaking space); four digits stay together ("1262"), as Spanish writes them.</summary>
    private static string Grouped(int n) => n < 10_000 ? n.ToString(Culture) : n.ToString("#,0", Culture).Replace(',', Space);
}
