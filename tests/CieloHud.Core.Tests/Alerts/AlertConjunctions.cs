using static CieloHud.Core.Tests.Alerts.AlertPasses;

namespace CieloHud.Core.Tests.Alerts;

/// <summary>Synthetic conjunctions for the alert tests, in local Madrid times.</summary>
internal static class AlertConjunctions
{
    /// <summary>
    /// The Moon next to <paramref name="planet"/> from <paramref name="start"/> to <paramref name="end"/>, best at <paramref name="best"/>
    /// with <paramref name="separation"/>, the Moon at 30° in the direction of <paramref name="moonAzimuth"/> (SE by default).
    /// The closest approach is the best moment, and the Moon is in the SW when the window closes.
    /// </summary>
    public static Conjunction Conjunction(
        CelestialBody planet, string start, string best, string end, double separation = 3, double moonAzimuth = 135)
    {
        ConjunctionPoint Point(string local, double azimuth, double sep) =>
            new(Local(local), new HorizontalPosition(azimuth, 30), new HorizontalPosition(azimuth, 30 + sep), sep);

        var bestPoint = Point(best, moonAzimuth, separation);
        return new Conjunction(CelestialBody.Moon, planet, Point(start, 100, separation + 1), bestPoint, bestPoint, Point(end, 225, separation + 1));
    }
}
