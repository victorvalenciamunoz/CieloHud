namespace CieloHud.Core.Tests.Alerts;

/// <summary>Synthetic visible passes and the observer's time zone for the alert tests.</summary>
internal static class AlertPasses
{
    /// <summary>IANA id: resolves on Linux (CI) and on Windows through ICU.</summary>
    public static readonly TimeZoneInfo Madrid = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");

    /// <summary>Instant from a local Madrid time, e.g. "2026-10-14 21:43".</summary>
    public static DateTimeOffset Local(string localTime)
    {
        var local = DateTime.Parse(localTime, System.Globalization.CultureInfo.InvariantCulture);
        return new DateTimeOffset(local, Madrid.GetUtcOffset(local));
    }

    /// <summary>
    /// A pass rising in the NW at the visible start, peaking at 67° in the SE three minutes later and visible for five minutes.
    /// With <paramref name="fromShadow"/> it lights up mid-sky one minute after rising, at 20° in the SW;
    /// with <paramref name="appearsAtMax"/> it lights up at its highest, three minutes after rising.
    /// </summary>
    public static VisiblePass Pass(
        DateTimeOffset visibleStart,
        TimeSpan? visibleDuration = null,
        bool fromShadow = false,
        double maxAltitude = 67,
        bool appearsAtMax = false)
    {
        var duration = visibleDuration ?? TimeSpan.FromMinutes(5);
        var rise = appearsAtMax ? visibleStart - TimeSpan.FromMinutes(3)
            : fromShadow ? visibleStart - TimeSpan.FromMinutes(1) : visibleStart;
        var start = new PassPoint(rise, new HorizontalPosition(315, 0));
        var max = new PassPoint(appearsAtMax ? visibleStart : visibleStart + TimeSpan.FromMinutes(3), new HorizontalPosition(135, maxAltitude));
        var end = new PassPoint(visibleStart + TimeSpan.FromMinutes(7), new HorizontalPosition(100, 0));
        var geometric = new SatellitePass(start, max, end);

        var visibleStartPoint = appearsAtMax
            ? max
            : fromShadow ? new PassPoint(visibleStart, new HorizontalPosition(225, 20)) : start;
        var visibleEnd = new PassPoint(visibleStart + duration, new HorizontalPosition(110, 15));
        return new VisiblePass(geometric, visibleStartPoint, max, visibleEnd);
    }
}
