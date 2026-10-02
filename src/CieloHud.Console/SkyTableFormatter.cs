using System.Globalization;
using System.Text;
using CieloHud.Core.Satellites;
using CieloHud.Core.Sky;
using CieloHud.Core.SolarSystem;

namespace CieloHud.Console;

/// <summary>
/// Turns positions into the text table. Spanish labels, decimal point, no calculations.
/// </summary>
public static class SkyTableFormatter
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Header(Observer observer, DateTimeOffset instant)
    {
        var local = instant.ToLocalTime();
        var sb = new StringBuilder();
        sb.AppendLine($"Observador: lat {observer.LatitudeDegrees.ToString("F4", Culture)}  lon {observer.LongitudeDegrees.ToString("F4", Culture)}  alt {observer.AltitudeMeters.ToString("F0", Culture)} m");
        sb.AppendLine($"Instante:   {instant.ToUniversalTime():yyyy-MM-dd HH:mm:ss} UTC  ({local:yyyy-MM-dd HH:mm:ss} hora local, UTC{local.Offset.Hours:+0;-0})");
        return sb.ToString();
    }

    public static string Table(IEnumerable<SkyRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{"Objeto",-9} {"Acimut",8} {"",-2} {"Altura",8}  {"Estado",-18} {"Distancia",12}");
        sb.AppendLine(new string('-', 9 + 1 + 8 + 1 + 2 + 1 + 8 + 2 + 18 + 1 + 12));
        foreach (var row in rows)
            sb.AppendLine(Row(row));
        return sb.ToString();
    }

    public static string Row(SkyRow row)
    {
        if (row.Position is not { } p)
            return $"{row.Name,-9} {"-",8} {"",-2} {"-",8}  {row.Note ?? "no disponible",-18} {"-",12}";

        var azimuth = $"{p.AzimuthDegrees.ToString("F2", Culture)}°";
        var altitude = $"{p.AltitudeDegrees.ToString("F2", Culture)}°";
        var state = p.IsAboveHorizon ? "sobre el horizonte" : "bajo el horizonte";
        return $"{row.Name,-9} {azimuth,8} {Cardinal(p.CardinalPoint),-2} {altitude,8}  {state,-18} {Distance(p.DistanceKm),12}";
    }

    public static string Name(CelestialBody body) => body switch
    {
        CelestialBody.Moon => "Luna",
        CelestialBody.Mercury => "Mercurio",
        CelestialBody.Venus => "Venus",
        CelestialBody.Mars => "Marte",
        CelestialBody.Jupiter => "Júpiter",
        CelestialBody.Saturn => "Saturno",
        _ => body.ToString(),
    };

    /// <summary>Spanish abbreviations: W is "O" (Oeste).</summary>
    public static string Cardinal(CardinalPoint point) => point switch
    {
        CardinalPoint.N => "N",
        CardinalPoint.NE => "NE",
        CardinalPoint.E => "E",
        CardinalPoint.SE => "SE",
        CardinalPoint.S => "S",
        CardinalPoint.SW => "SO",
        CardinalPoint.W => "O",
        CardinalPoint.NW => "NO",
        _ => point.ToString(),
    };

    public static string TleInfo(Tle tle, DateTimeOffset instant)
    {
        var age = instant - tle.Epoch;
        return $"TLE ISS: época {tle.Epoch:yyyy-MM-dd HH:mm} UTC ({age.TotalHours.ToString("F1", Culture)} h antes del instante)";
    }

    private static string Distance(double? km) => km switch
    {
        null => "-",
        < 10_000 => $"{km.Value.ToString("F0", Culture)} km",
        < 10_000_000 => $"{(km.Value / 1000).ToString("F0", Culture)} mil km",
        _ => $"{(km.Value / 1_000_000).ToString("F1", Culture)} M km",
    };
}

/// <summary>One line of the table. Position is null when it could not be computed; Note says why.</summary>
public sealed record SkyRow(string Name, HorizontalPosition? Position, string? Note = null);
