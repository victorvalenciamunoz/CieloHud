using System.Globalization;
using System.Text;
using CieloHud.Core.Apparitions;
using CieloHud.Core.Sky;

namespace CieloHud.Console;

/// <summary>
/// Mercury's seasons as a table in local time (the days it can be seen and its best day), and every day's window.
/// Spanish labels, decimal point, no calculations.
/// </summary>
public static class MercuryTableFormatter
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Header(Observer observer, DateTimeOffset from, DateTimeOffset to, MercuryCriteria criteria)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Observador: lat {observer.LatitudeDegrees.ToString("F4", Culture)}  lon {observer.LongitudeDegrees.ToString("F4", Culture)}  alt {observer.AltitudeMeters.ToString("F0", Culture)} m");
        sb.AppendLine($"Mercurio del {from.ToLocalTime():yyyy-MM-dd HH:mm} al {to.ToLocalTime():yyyy-MM-dd HH:mm} (hora local)");
        sb.AppendLine($"Criterio: Mercurio a ≥ {criteria.MinAltitudeDegrees.ToString("F0", Culture)}°, Sol por debajo de {criteria.MaxSunAltitudeDegrees.ToString("F0", Culture)}°, " +
            $"magnitud ≤ {criteria.MaxMagnitude.ToString("F1", Culture)}");
        return sb.ToString();
    }

    /// <summary>Each season: its days and its best day.</summary>
    public static string Apparitions(IEnumerable<MercuryApparition> apparitions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Temporadas (por su mejor día):");
        sb.AppendLine($"{"Cuándo",-10} {"Días",-18} {"Mejor día",-10} {"Ventana",-11}  {"Mejor momento: hora alt/acimut  mag.",-36}");
        sb.AppendLine(new string('-', 10 + 1 + 18 + 1 + 10 + 1 + 11 + 2 + 36));
        var any = false;
        foreach (var a in apparitions)
        {
            any = true;
            var days = $"{Day(a.First.Best.Instant)} a {Day(a.Last.Best.Instant)} ({a.Days})";
            sb.AppendLine($"{Period(a.Period),-10} {days,-18} {Date(a.Best.Best.Instant),-10} {Window(a.Best),-11}  {BestText(a.Best)}");
        }
        if (!any)
            sb.AppendLine("(ninguna)");
        return sb.ToString();
    }

    /// <summary>Every day's window, for checking against other ephemerides.</summary>
    public static string Windows(IEnumerable<MercuryWindow> windows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Día a día:");
        sb.AppendLine($"{"Fecha",-10} {"Cuándo",-10} {"Ventana",-11}  {"min",3}  {"Mejor momento: hora alt/acimut  mag.",-36}");
        var any = false;
        foreach (var w in windows)
        {
            any = true;
            var minutes = Math.Round(w.Duration.TotalMinutes).ToString("F0", Culture);
            sb.AppendLine($"{Date(w.Best.Instant),-10} {Period(w.Period),-10} {Window(w),-11}  {minutes,3}  {BestText(w)}");
        }
        if (!any)
            sb.AppendLine("(ninguno)");
        return sb.ToString();
    }

    private static string BestText(MercuryWindow w)
    {
        var p = w.Best.Mercury;
        return $"{Time(w.Best.Instant)}  {p.AltitudeDegrees.ToString("F1", Culture),4}° {Math.Round(p.AzimuthDegrees).ToString("F0", Culture),3}° " +
            $"{SkyTableFormatter.Cardinal(p.CardinalPoint),-2}  {w.Magnitude.ToString("F1", Culture),4}";
    }

    private static string Period(TwilightPeriod period) => period == TwilightPeriod.Dusk ? "anochecer" : "amanecer";

    private static string Window(MercuryWindow w) => $"{Time(w.Start.Instant)}-{Time(w.End.Instant)}";

    private static string Date(DateTimeOffset instant) => instant.ToLocalTime().ToString("yyyy-MM-dd", Culture);

    private static string Day(DateTimeOffset instant) => instant.ToLocalTime().ToString("dd/MM", Culture);

    private static string Time(DateTimeOffset instant) => instant.ToLocalTime().ToString("HH:mm", Culture);
}
