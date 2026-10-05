using CieloHud.Core.Passes;
using static CieloHud.Core.Alerts.AlertWords;

namespace CieloHud.Core.Alerts;

/// <summary>
/// The words of a pass alert, in Spanish (decision 021): "A las 21:43 pasa la ISS · 5 min · aparece por el NO, máximo 67° al SE".
/// Times in the observer's local time; degrees rounded to whole numbers, like Heavens-Above.
/// </summary>
public static class PassAlertText
{
    /// <summary>Short headline, relative to when the alert goes off: "La ISS pasa en 10 min" or "Mañana temprano pasa la ISS".</summary>
    public static string Title(PassAlert alert)
    {
        if (alert.IsEveningBefore)
            return "Mañana temprano pasa la ISS";
        var minutes = Math.Max(1, Round((alert.Pass.VisibleStart.Instant - alert.NotifyAt).TotalMinutes));
        return $"La ISS pasa en {minutes} min";
    }

    /// <summary>When, for how long and where to look.</summary>
    public static string Body(PassAlert alert, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var pass = alert.Pass;
        return $"{When(alert, timeZone)} pasa la ISS · {Minutes(pass)} min · {Where(pass)}";
    }

    private static string When(PassAlert alert, TimeZoneInfo timeZone)
    {
        var start = TimeZoneInfo.ConvertTime(alert.Pass.VisibleStart.Instant, timeZone);
        var notify = TimeZoneInfo.ConvertTime(alert.NotifyAt, timeZone);
        var time = TheTime(start);
        return (start.Date - notify.Date).Days switch
        {
            0 => $"A {time}",
            1 => $"Mañana a {time}",
            _ => $"El día {start.Day.ToString(Culture)} a {time}",
        };
    }

    // Whole minutes of visible time, at least one: a short pass is still worth going out for.
    internal static int Minutes(VisiblePass pass) => Math.Max(1, Round(pass.VisibleDuration.TotalMinutes));

    internal static string Where(VisiblePass pass)
    {
        var start = pass.VisibleStart.Position;
        // Rising from the horizon you look low in that direction; emerging from shadow it lights up mid-sky.
        var appears = pass.StartsFromShadow
            ? $"aparece a {Degrees(start.AltitudeDegrees)}° al {Cardinal(start.CardinalPoint)}"
            : $"aparece por el {Cardinal(start.CardinalPoint)}";

        // Lit only after culminating: it appears at its highest and goes down from there.
        if (pass.VisibleMax.Instant == pass.VisibleStart.Instant)
            return $"{appears} y va bajando";

        var max = pass.VisibleMax.Position;
        return $"{appears}, máximo {Degrees(max.AltitudeDegrees)}° al {Cardinal(max.CardinalPoint)}";
    }
}
