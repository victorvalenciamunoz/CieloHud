using System.Globalization;
using System.Text;
using CieloHud.Core.Cards;
using CieloHud.Core.Constellations;

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

    /// <summary>The ISS's lines as the card shows them, then the raw height, distance, speed and period.</summary>
    public static string Iss(SatelliteFacts iss, CardText? text)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ISS");
        AppendText(sb, text);
        sb.AppendLine($"  {FactsText.SatelliteAltitude(iss)}");
        sb.AppendLine($"  {FactsText.SatelliteSpeed(iss)}");
        sb.AppendLine($"  {FactsText.SatelliteVisibility(iss)}");
        sb.AppendLine();
        sb.AppendLine($"  Altura sobre el elipsoide: {iss.AltitudeKm.ToString("F2", Culture)} km");
        sb.AppendLine($"  Distancia al observador: {iss.DistanceKm.ToString("F1", Culture)} km");
        sb.AppendLine($"  Velocidad (inercial): {iss.SpeedKmPerSecond.ToString("F4", Culture)} km/s = {(iss.SpeedKmPerSecond * 3600).ToString("F0", Culture)} km/h");
        sb.AppendLine($"  Periodo: {iss.Period.TotalMinutes.ToString("F3", Culture)} min · {iss.Sight}");
        return sb.ToString();
    }

    /// <summary>The star's line as the card shows it (with its IAU name; the app uses the Spanish one), then the raw parallax.</summary>
    public static string Star(CieloHud.Core.Stars.Star star, CardText? text)
    {
        var light = StarLight.Of(star);
        var sb = new StringBuilder();
        sb.AppendLine($"{star.Name} ({star.Designation}, HIP {star.Hipparcos.ToString(Culture)})");
        AppendText(sb, text);
        sb.AppendLine($"  {FactsText.StarLight(star.Name, light)}");
        sb.AppendLine();
        sb.AppendLine($"  Paralaje (Hipparcos, van Leeuwen 2007): {star.ParallaxMas.ToString("F2", Culture)} ± {star.ParallaxErrorMas.ToString("F2", Culture)} mas " +
            $"({(star.ParallaxErrorMas / star.ParallaxMas * 100).ToString("F1", Culture)} %)");
        sb.AppendLine($"  Distancia: {light.Years.ToString("F1", Culture)} años luz, de {light.NearYears.ToString("F1", Culture)} a " +
            $"{(double.IsInfinity(light.FarYears) ? "∞" : light.FarYears.ToString("F1", Culture))} · {light.Certainty}");
        return sb.ToString();
    }

    /// <summary>
    /// The constellation's lines as the card shows them (with the stars' IAU names; the app uses the Spanish ones), then its raw
    /// extent and the latitudes from which it rises whole, to compare with Ridpath's table.
    /// </summary>
    public static string Constellation(ConstellationFacts facts, ConstellationExtent extent, CardText? text)
    {
        var sb = new StringBuilder();
        sb.AppendLine(facts.Symbol);
        AppendText(sb, text);
        foreach (var line in FactsText.Constellation(facts, facts.BrightestStars?.Select(s => s.Name).ToList()))
            sb.AppendLine($"  {line}");
        sb.AppendLine();
        sb.AppendLine($"  Área: {extent.AreaSquareDegrees.ToString("F2", Culture)} grados cuadrados " +
            $"({(facts.SkyFraction * 100).ToString("F3", Culture)} % del cielo), puesto {facts.SizeRank} de 88");
        sb.AppendLine($"  Declinación (J2000): de {extent.MinDeclinationDegrees.ToString("F2", Culture)}° a {extent.MaxDeclinationDegrees.ToString("F2", Culture)}°");
        sb.AppendLine($"  Sale entera desde latitudes entre {Latitude(Math.Max(-90, extent.MaxDeclinationDegrees - 90))} y " +
            $"{Latitude(Math.Min(90, extent.MinDeclinationDegrees + 90))} · {facts.Sight}");
        sb.AppendLine(facts.BrightestStars is { } stars
            ? $"  Estrellas del catálogo: {(stars.Count == 0 ? "ninguna" : string.Join(", ", stars.Select(s => $"{s.Name} {s.Magnitude.ToString("F2", Culture)}")))}"
            : "  Estrellas del catálogo: no cubre esta constelación (llega al sur de -50°)");
        return sb.ToString();

        static string Latitude(double degrees) =>
            degrees == 0 ? "0°" : $"{Math.Abs(degrees).ToString("F1", Culture)}° {(degrees > 0 ? "N" : "S")}";
    }

    /// <summary>
    /// What the drawing uses, to compare with Stellarium in an altazimuth mount: where it is centred, each star in degrees from the
    /// center (right, up) and the direction from the brightest to the others (0° towards the zenith, 90° to the right).
    /// </summary>
    public static string ConstellationDrawing(ConstellationShape shape)
    {
        var sb = new StringBuilder();
        var c = shape.Center;
        sb.AppendLine("  Dibujo (0° hacia el cénit, 90° a la derecha; grados de cielo desde el centro):");
        sb.AppendLine($"    Centro: acimut {c.AzimuthDegrees.ToString("F1", Culture)}° {SkyTableFormatter.Cardinal(c.CardinalPoint)}, " +
            $"altura {c.AltitudeDegrees.ToString("F1", Culture)}°");
        sb.AppendLine($"    Tamaño: {shape.Bounds.Width.ToString("F1", Culture)}° de ancho y {shape.Bounds.Height.ToString("F1", Culture)}° de alto; " +
            $"{shape.Segments.Count} trazos ({shape.Segments.Count(s => !s.AboveHorizon)} bajo el horizonte), {shape.FaintVertices.Count} vértices sin estrella del catálogo");
        foreach (var star in shape.Stars)
            sb.AppendLine($"    {star.Star.Name,-14} {star.Star.Magnitude.ToString("F2", Culture),5}  derecha {star.At.Right.ToString("F1", Culture),6}  " +
                $"arriba {star.At.Up.ToString("F1", Culture),6}{(star.Labelled ? "  rótulo" : "")}{(star.AboveHorizon ? "" : "  bajo el horizonte")}");
        if (shape.Stars.Count > 1)
        {
            var first = shape.Stars[0];
            foreach (var other in shape.Stars.Skip(1).Where(s => s.Labelled))
            {
                var direction = (Math.Atan2(other.At.Right - first.At.Right, other.At.Up - first.At.Up) * 180 / Math.PI + 360) % 360;
                sb.AppendLine($"    De {first.Star.Name} a {other.Star.Name}: hacia {direction.ToString("F1", Culture)}°");
            }
        }
        return sb.ToString();
    }

    /// <summary>What the drawing uses: lit fraction, bright limb and north pole, in the HUD's axes.</summary>
    public static string Disc(BodyDisc disc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("  Dibujo (0° hacia el cénit, 90° a la derecha):");
        sb.AppendLine($"    Iluminada: {(disc.IlluminatedFraction * 100).ToString("F3", Culture)} %");
        sb.AppendLine($"    Borde iluminado hacia: {disc.BrightLimbDegrees.ToString("F2", Culture)}°");
        sb.AppendLine($"    Polo norte hacia: {disc.NorthPoleDegrees.ToString("F2", Culture)}°");
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
