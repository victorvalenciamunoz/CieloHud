using CieloHud.Core.Apparitions;

namespace CieloHud.Core.Alerts;

/// <summary>An alert for a season of Mercury, on its best day (decision 034).</summary>
/// <param name="NotifyAt">When the alert goes off (UTC). Equal to the planning instant when it is already due.</param>
/// <param name="IsEveningBefore">True when it would be announced in the quiet hours and is moved to the evening before.</param>
public sealed record MercuryAlert(MercuryApparition Apparition, DateTimeOffset NotifyAt, bool IsEveningBefore)
{
    /// <summary>The best day's window: the one the alert recommends.</summary>
    public MercuryWindow Window => Apparition.Best;

    /// <summary>When the best day's window opens.</summary>
    public DateTimeOffset WindowStart => Window.Start.Instant;

    /// <summary>When the best day's window closes: the alert is pointless after it.</summary>
    public DateTimeOffset WindowEnd => Window.End.Instant;
}

/// <summary>A season of Mercury already announced, enough to recognize it when planning again: dusk or dawn, and its best moment.</summary>
public readonly record struct NotifiedMercury(TwilightPeriod Period, DateTimeOffset Best)
{
    public static NotifiedMercury From(MercuryApparition apparition) => new(apparition.Period, apparition.Best.Best.Instant);
}
