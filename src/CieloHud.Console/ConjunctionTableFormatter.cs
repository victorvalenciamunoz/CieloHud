using System.Globalization;
using System.Text;
using CieloHud.Core.Alerts;
using CieloHud.Core.Conjunctions;
using CieloHud.Core.Sky;

namespace CieloHud.Console;

/// <summary>
/// Moon-planet conjunctions as a table in local time (the window, the best moment and the closest approach), and their alerts.
/// Spanish labels, decimal point, no calculations.
/// </summary>
public static class ConjunctionTableFormatter
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Header(Observer observer, DateTimeOffset from, DateTimeOffset to, ConjunctionCriteria criteria)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Observador: lat {observer.LatitudeDegrees.ToString("F4", Culture)}  lon {observer.LongitudeDegrees.ToString("F4", Culture)}  alt {observer.AltitudeMeters.ToString("F0", Culture)} m");
        sb.AppendLine($"Conjunciones del {from.ToLocalTime():yyyy-MM-dd HH:mm} al {to.ToLocalTime():yyyy-MM-dd HH:mm} (hora local)");
        sb.AppendLine($"Criterio: separación ≤ {criteria.MaxMoonSeparationDegrees.ToString("F0", Culture)}° con la Luna y ≤ {criteria.MaxPlanetSeparationDegrees.ToString("F0", Culture)}° entre planetas, los dos a ≥ {criteria.MinAltitudeDegrees.ToString("F0", Culture)}°, " +
            $"Sol por debajo de {criteria.MaxSunAltitudeDegrees.ToString("F0", Culture)}°; planetas: {string.Join(", ", criteria.Planets.Select(SkyTableFormatter.Name))}");
        return sb.ToString();
    }

    public static string Table(IEnumerable<Conjunction> conjunctions)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{"Fecha",-10} {"Planeta",-8} {"Ventana",-11}  {"Mejor momento",-38}  Más cerca");
        sb.AppendLine($"{"",-10} {"",-8} {"",-11}  {"hora   sep.   Luna alt/acimut  planeta",-38}  hora   sep.  más bajo");
        sb.AppendLine(new string('-', 10 + 1 + 8 + 1 + 11 + 2 + 38 + 2 + 24));
        var any = false;
        foreach (var conjunction in conjunctions)
        {
            any = true;
            sb.AppendLine(Row(conjunction));
        }
        if (!any)
            sb.AppendLine("(ninguna)");
        return sb.ToString();
    }

    /// <summary>Two planets together: the closest night of each approach, and every night they are close.</summary>
    public static string PlanetPairs(IEnumerable<Conjunction> conjunctions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Planetas juntos (la noche más cercana de cada acercamiento):");
        sb.AppendLine($"{"Fecha",-10} {"Pareja",-16} {"Ventana",-11}  {"hora   sep.   guía alt/acimut   otro",-38}  Noches juntos");
        sb.AppendLine(new string('-', 10 + 1 + 16 + 1 + 11 + 2 + 38 + 2 + 16));
        var any = false;
        foreach (var c in conjunctions)
        {
            any = true;
            var date = c.Best.Instant.ToLocalTime().ToString("yyyy-MM-dd", Culture);
            var pair = $"{SkyTableFormatter.Name(c.Guide)}-{SkyTableFormatter.Name(c.Companion)}";
            var window = $"{Time(c.Start.Instant)}-{Time(c.End.Instant)}";
            var nights = c.Nights is { } n ? $"{Day(n.First)} a {Day(n.Last)}" : "";
            sb.AppendLine($"{date,-10} {pair,-16} {window,-11}  {BestText(c.Best),-38}  {nights}");
        }
        if (!any)
            sb.AppendLine("(ninguna)");
        return sb.ToString();
    }

    /// <summary>The alerts the app would post for these conjunctions, planned from the start of the listing.</summary>
    public static string Alerts(IEnumerable<ConjunctionAlert> alerts, TimeZoneInfo timeZone)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Avisos (planificados desde el inicio del listado):");
        var any = false;
        foreach (var alert in alerts)
        {
            any = true;
            var at = TimeZoneInfo.ConvertTime(alert.NotifyAt, timeZone).ToString("yyyy-MM-dd HH:mm", Culture);
            sb.AppendLine($"{at}  {ConjunctionAlertText.Title(alert)} · {ConjunctionAlertText.Body(alert, timeZone)}");
        }
        if (!any)
            sb.AppendLine("(ninguno)");
        return sb.ToString();
    }

    public static string Row(Conjunction c)
    {
        var date = c.Best.Instant.ToLocalTime().ToString("yyyy-MM-dd", Culture);
        var window = $"{Time(c.Start.Instant)}-{Time(c.End.Instant)}";
        var bestText = BestText(c.Best);
        var closest = $"{Time(c.Closest.Instant)}  {Separation(c.Closest)}  {Degrees(c.Closest.LowerAltitudeDegrees),3}°";
        return $"{date,-10} {SkyTableFormatter.Name(c.Companion),-8} {window,-11}  {bestText,-38}  {closest}";
    }

    private static string BestText(ConjunctionPoint best)
    {
        var guide = $"{Degrees(best.Guide.AltitudeDegrees),3}° {Degrees(best.Guide.AzimuthDegrees),3}° {SkyTableFormatter.Cardinal(best.Guide.CardinalPoint),-2}";
        return $"{Time(best.Instant)}  {Separation(best)}  {guide}    {Degrees(best.Companion.AltitudeDegrees),3}°";
    }

    private static string Day(DateTimeOffset instant) => instant.ToLocalTime().ToString("dd/MM", Culture);

    private static string Time(DateTimeOffset instant) => instant.ToLocalTime().ToString("HH:mm", Culture);

    private static string Separation(ConjunctionPoint point) => point.SeparationDegrees.ToString("F2", Culture) + "°";

    private static string Degrees(double degrees) => Math.Round(degrees).ToString("F0", Culture);
}
