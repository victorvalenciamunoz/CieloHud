using System.Globalization;
using System.Text;
using CieloHud.Core.Stars;

namespace CieloHud.Console;

/// <summary>What the STARS chip offers (decision 054), with the positions to compare with Stellarium. No calculations.</summary>
public static class StarChoicesFormatter
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Brightest(IReadOnlyList<StarChoice> list, bool daylight)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Las más brillantes a más de {StarChoices.MinAltitudeDegrees.ToString(Culture)}° (como mucho {StarChoices.MaxShown}, por orden alfabético)");
        AppendDaylight(sb, daylight);
        if (list.Count == 0)
            sb.AppendLine("  Ninguna tan alta.");
        AppendRows(sb, list);
        return sb.ToString();
    }

    public static string Search(string query, StarSearchResult result, bool daylight)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Búsqueda «{query}»: {result.Total} (se muestran {result.Shown.Count}, por orden alfabético)");
        AppendDaylight(sb, daylight);
        AppendRows(sb, result.Shown);
        if (result.HasMore)
            sb.AppendLine($"  … y {result.Total - result.Shown.Count} más: sigue escribiendo");
        return sb.ToString();
    }

    private static void AppendDaylight(StringBuilder sb, bool daylight)
    {
        if (daylight)
            sb.AppendLine($"  Es de día (Sol por encima de {StarChoices.MaxSunAltitudeDegrees.ToString(Culture)}°): las estrellas no se ven.");
    }

    private static void AppendRows(StringBuilder sb, IEnumerable<StarChoice> choices)
    {
        foreach (var c in choices)
        {
            var where = c.AboveHorizon ? "" : "  bajo el horizonte";
            sb.AppendLine(
                $"  {c.Name,-22} {c.Star.Designation,-10} en {c.Constellation,-22} mag {c.Star.Magnitude.ToString("F2", Culture),5}  " +
                $"alt {c.Position.AltitudeDegrees.ToString("F1", Culture),5}°  az {c.Position.AzimuthDegrees.ToString("F1", Culture),5}° {SkyTableFormatter.Cardinal(c.Position.CardinalPoint),-2}{where}");
        }
    }
}
