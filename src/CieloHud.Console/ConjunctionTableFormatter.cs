using System.Globalization;
using System.Text;
using CieloHud.Core.Conjunctions;
using CieloHud.Core.Sky;

namespace CieloHud.Console;

/// <summary>
/// Moon-planet conjunctions as a table in local time: the window, the best moment and the closest approach.
/// Spanish labels, decimal point, no calculations.
/// </summary>
public static class ConjunctionTableFormatter
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Header(Observer observer, DateTimeOffset from, DateTimeOffset to, ConjunctionCriteria criteria)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Observador: lat {observer.LatitudeDegrees.ToString("F4", Culture)}  lon {observer.LongitudeDegrees.ToString("F4", Culture)}  alt {observer.AltitudeMeters.ToString("F0", Culture)} m");
        sb.AppendLine($"Conjunciones Luna-planeta del {from.ToLocalTime():yyyy-MM-dd HH:mm} al {to.ToLocalTime():yyyy-MM-dd HH:mm} (hora local)");
        sb.AppendLine($"Criterio: separación ≤ {criteria.MaxSeparationDegrees.ToString("F0", Culture)}°, los dos a ≥ {criteria.MinAltitudeDegrees.ToString("F0", Culture)}°, " +
            $"Sol por debajo de {criteria.MaxSunAltitudeDegrees.ToString("F0", Culture)}°; planetas: {string.Join(", ", criteria.Planets.Select(SkyTableFormatter.Name))}");
        return sb.ToString();
    }

    public static string Table(IEnumerable<MoonPlanetConjunction> conjunctions)
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

    public static string Row(MoonPlanetConjunction c)
    {
        var date = c.Best.Instant.ToLocalTime().ToString("yyyy-MM-dd", Culture);
        var window = $"{Time(c.Start.Instant)}-{Time(c.End.Instant)}";
        var best = c.Best;
        var moon = $"{Degrees(best.Moon.AltitudeDegrees),3}° {Degrees(best.Moon.AzimuthDegrees),3}° {SkyTableFormatter.Cardinal(best.Moon.CardinalPoint),-2}";
        var bestText = $"{Time(best.Instant)}  {Separation(best)}  {moon}    {Degrees(best.Planet.AltitudeDegrees),3}°";
        var closest = $"{Time(c.Closest.Instant)}  {Separation(c.Closest)}  {Degrees(c.Closest.LowerAltitudeDegrees),3}°";
        return $"{date,-10} {SkyTableFormatter.Name(c.Planet),-8} {window,-11}  {bestText,-38}  {closest}";
    }

    private static string Time(DateTimeOffset instant) => instant.ToLocalTime().ToString("HH:mm", Culture);

    private static string Separation(ConjunctionPoint point) => point.SeparationDegrees.ToString("F2", Culture) + "°";

    private static string Degrees(double degrees) => Math.Round(degrees).ToString("F0", Culture);
}
