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

    public static string Moon(MoonFacts moon, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Luna");
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

    public static string Planet(string name, PlanetFacts planet)
    {
        var sb = new StringBuilder();
        sb.AppendLine(name);
        sb.AppendLine($"  {FactsText.PlanetDistance(planet)}");
        sb.AppendLine($"  {FactsText.PlanetLight(planet)}");
        sb.AppendLine();
        sb.AppendLine($"  Distancia: {planet.DistanceKm.ToString("F0", Culture)} km ({(planet.DistanceKm / 149_597_870.7).ToString("F8", Culture)} ua)");
        sb.AppendLine($"  Tiempo de luz: {planet.LightTime.TotalMinutes.ToString("F5", Culture)} min");
        return sb.ToString();
    }
}
