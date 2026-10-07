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
