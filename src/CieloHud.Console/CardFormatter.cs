using System.Globalization;
using System.Text;
using CieloHud.Core.Cards;

namespace CieloHud.Console;

/// <summary>
/// The facts of a card as the app will show them, followed by the raw values to compare with Horizons or Stellarium.
/// No calculations.
/// </summary>
public static class CardFormatter
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Moon(MoonFacts moon, CardText? text, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Luna");
        AppendText(sb, text);
        sb.AppendLine($"  {FactsText.MoonPhase(moon)}");
        sb.AppendLine($"  {FactsText.NextQuarter(moon.Next, now, timeZone)}");
        sb.AppendLine($"  {FactsText.MoonDistance(moon)}");
        sb.AppendLine();
        sb.AppendLine($"  Iluminada (topocéntrica): {(moon.IlluminatedFraction * 100).ToString("F3", Culture)} %");
        sb.AppendLine($"  Fase (Luna - Sol, geocéntrica): {moon.PhaseDegrees.ToString("F3", Culture)}°");
        sb.AppendLine($"  Próxima fase principal: {moon.Next.Instant.ToUniversalTime():yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"  Distancia: {moon.DistanceKm.ToString("F1", Culture)} km · luz {moon.LightTime.TotalSeconds.ToString("F3", Culture)} s");
        return sb.ToString();
    }

    public static string Planet(string name, PlanetFacts planet, CardText? text)
    {
        var sb = new StringBuilder();
        sb.AppendLine(name);
        AppendText(sb, text);
        sb.AppendLine($"  {FactsText.PlanetDistance(planet)}");
        sb.AppendLine($"  {FactsText.PlanetLight(planet)}");
        sb.AppendLine();
        sb.AppendLine($"  Distancia: {planet.DistanceKm.ToString("F0", Culture)} km ({(planet.DistanceKm / 149_597_870.7).ToString("F8", Culture)} ua)");
        sb.AppendLine($"  Tiempo de luz: {planet.LightTime.TotalMinutes.ToString("F5", Culture)} min");
        return sb.ToString();
    }

    /// <summary>The rings' lines as the card shows them, then the raw tilt and trend.</summary>
    public static string SaturnRings(SaturnRingsFacts rings, DateTimeOffset now)
    {
        var sb = new StringBuilder();
        foreach (var line in FactsText.SaturnRings(rings, now))
            sb.AppendLine($"  {line}");
        sb.AppendLine();
        sb.AppendLine($"  Inclinación vista desde la Tierra: {rings.TiltDegrees.ToString("F3", Culture)}° (+ cara norte, - cara sur)");
        sb.AppendLine($"  Tendencia: {rings.Trend} hasta {rings.Until:yyyy-MM-dd} ({rings.TiltAtUntilDegrees.ToString("F2", Culture)}° vista desde el Sol)");
        return sb.ToString();
    }

    /// <summary>The moons' lines as the card shows them, then each moon's offset in Jupiter radii and arcseconds.</summary>
    public static string JupiterMoons(JupiterMoonsFacts moons)
    {
        var sb = new StringBuilder();
        foreach (var line in FactsText.JupiterMoons(moons))
            sb.AppendLine($"  {line}");
        sb.AppendLine();
        sb.AppendLine($"  Radio aparente de Júpiter: {moons.JupiterRadiusArcseconds.ToString("F2", Culture)}\"");
        sb.AppendLine($"  {"Luna",-10} {"derecha",9} {"arriba",9} {"(radios)",-10} {"derecha",9} {"arriba",9} {"(\")",-4} estado");
        foreach (var m in moons.Moons)
        {
            var k = moons.JupiterRadiusArcseconds;
            sb.AppendLine($"  {FactsText.MoonName(m.Name),-10} {m.RightRadii.ToString("F3", Culture),9} {m.UpRadii.ToString("F3", Culture),9} {"",-10} " +
                $"{(m.RightRadii * k).ToString("F2", Culture),9} {(m.UpRadii * k).ToString("F2", Culture),9} {"",-4} {m.State}");
        }
        return sb.ToString();
    }

    /// <summary>The written text, wrapped, then a blank line; a note when it is not written yet.</summary>
    private static void AppendText(StringBuilder sb, CardText? text)
    {
        if (text is null)
        {
            sb.AppendLine("  (sin texto todavía)");
            sb.AppendLine();
            return;
        }
        foreach (var paragraph in text.Body.Split("\n\n"))
        {
            // Wrapped at plain spaces only: non-breaking ones keep "282 000 km" together, as in the app.
            foreach (var line in Wrap(paragraph, 100))
                sb.AppendLine($"  {line.Replace(' ', ' ')}");
            sb.AppendLine();
        }
    }

    private static IEnumerable<string> Wrap(string text, int width)
    {
        var line = new StringBuilder();
        foreach (var word in text.Split(' '))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                yield return line.ToString();
                line.Clear();
            }
            if (line.Length > 0)
                line.Append(' ');
            line.Append(word);
        }
        if (line.Length > 0)
            yield return line.ToString();
    }
}
