using System.Globalization;
using System.Text;
using CieloHud.Core.Passes;
using CieloHud.Core.Sky;

namespace CieloHud.Console;

/// <summary>
/// Visible ISS passes as a Heavens-Above style table, in local time. Spanish labels, decimal point, no calculations.
/// </summary>
public static class PassTableFormatter
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Header(Observer observer, DateTimeOffset from, DateTimeOffset to, VisibilityCriteria criteria)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Observador: lat {observer.LatitudeDegrees.ToString("F4", Culture)}  lon {observer.LongitudeDegrees.ToString("F4", Culture)}  alt {observer.AltitudeMeters.ToString("F0", Culture)} m");
        sb.AppendLine($"Pasos visibles de la ISS del {from.ToLocalTime():yyyy-MM-dd HH:mm} al {to.ToLocalTime():yyyy-MM-dd HH:mm} (hora local, UTC{from.ToLocalTime().Offset.Hours:+0;-0})");
        sb.AppendLine($"Criterio: Sol por debajo de {criteria.MaxSunAltitudeDegrees.ToString("F0", Culture)}°, ISS iluminada, altura máxima ≥ {criteria.MinPeakAltitudeDegrees.ToString("F0", Culture)}°");
        return sb.ToString();
    }

    public static string Table(IEnumerable<VisiblePass> passes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{"Fecha",-10} {"Inicio",-20} {"Máximo",-20} {"Fin",-20} {"Dur.",5}  Nota");
        sb.AppendLine($"{"",-10} {"hora  alt  acimut",-20} {"hora  alt  acimut",-20} {"hora  alt  acimut",-20} {"min",5}");
        sb.AppendLine(new string('-', 10 + 1 + 20 + 1 + 20 + 1 + 20 + 1 + 5 + 2 + 24));
        var any = false;
        foreach (var pass in passes)
        {
            any = true;
            sb.AppendLine(Row(pass));
        }
        if (!any)
            sb.AppendLine("(ninguno)");
        return sb.ToString();
    }

    public static string Row(VisiblePass pass)
    {
        var date = pass.VisibleStart.Instant.ToLocalTime().ToString("yyyy-MM-dd", Culture);
        var duration = pass.VisibleDuration.TotalMinutes.ToString("F1", Culture);
        return $"{date,-10} {Point(pass.VisibleStart),-20} {Point(pass.VisibleMax),-20} {Point(pass.VisibleEnd),-20} {duration,5}  {Note(pass)}";
    }

    private static string Point(PassPoint point)
    {
        var time = point.Instant.ToLocalTime().ToString("HH:mm:ss", Culture);
        var altitude = WholeDegrees(point.Position.AltitudeDegrees).PadLeft(3) + "°";
        var azimuth = $"{WholeDegrees(point.Position.AzimuthDegrees).PadLeft(3)}° {SkyTableFormatter.Cardinal(point.Position.CardinalPoint),-2}";
        return $"{time} {altitude} {azimuth}";
    }

    // Avoids "-0" for values like -0.02 at the horizon.
    private static string WholeDegrees(double degrees)
    {
        var rounded = Math.Round(degrees);
        return (rounded == 0 ? 0 : rounded).ToString("F0", Culture);
    }

    private static string Note(VisiblePass pass) => (pass.StartsFromShadow, pass.EndsInShadow) switch
    {
        (true, true) => "aparece y se apaga en el cielo",
        (true, false) => "aparece a media altura (sale de la sombra)",
        (false, true) => "se apaga antes de ponerse (entra en sombra)",
        _ => "",
    };
}
