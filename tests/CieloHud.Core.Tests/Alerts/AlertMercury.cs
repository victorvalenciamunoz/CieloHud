using static CieloHud.Core.Tests.Alerts.AlertPasses;

namespace CieloHud.Core.Tests.Alerts;

/// <summary>Synthetic seasons of Mercury for the alert tests, in local Madrid times.</summary>
internal static class AlertMercury
{
    /// <summary>
    /// A season from <paramref name="firstDay"/> to <paramref name="lastDay"/> (best moments of those days) whose best day's window runs
    /// from <paramref name="start"/> to <paramref name="end"/>, best at <paramref name="best"/> with Mercury at <paramref name="altitude"/>
    /// toward <paramref name="azimuth"/>; it sets toward the west as the window closes.
    /// </summary>
    public static MercuryApparition Apparition(
        TwilightPeriod period, string start, string best, string end, string firstDay, string lastDay,
        double altitude = 12.4, double azimuth = 118, int days = 15)
    {
        MercuryPoint Point(string local, double alt, double az) => new(Local(local), new HorizontalPosition(az, alt), -6);
        MercuryWindow Window(string at) => new(period, Point(at, 10.5, azimuth), Point(at, 10.5, azimuth), Point(at, 10.5, azimuth), -0.4);

        var bestWindow = new MercuryWindow(period, Point(start, 10, azimuth), Point(best, altitude, azimuth), Point(end, 10, 250), -0.6);
        return new MercuryApparition(period, bestWindow, Window(firstDay), Window(lastDay), days);
    }

    /// <summary>The November 2026 dawn season, as found from Madrid: best on the 20th, 7:23-7:37, 12.4° to the SE.</summary>
    public static MercuryApparition NovemberDawn() => Apparition(TwilightPeriod.Dawn,
        "2026-11-20 07:23", "2026-11-20 07:37", "2026-11-20 07:37", "2026-11-14 07:30", "2026-11-28 07:45");

    /// <summary>The February 2027 dusk season: best on the 4th, 19:05-19:11, 11.1° to the SW.</summary>
    public static MercuryApparition FebruaryDusk() => Apparition(TwilightPeriod.Dusk,
        "2027-02-04 19:05", "2027-02-04 19:05", "2027-02-04 19:11", "2027-01-31 19:01", "2027-02-07 19:09", altitude: 11.1, azimuth: 247, days: 8);
}
