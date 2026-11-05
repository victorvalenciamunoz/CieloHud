namespace CieloHud.Core.Passes;

/// <summary>
/// The part of a pass that can actually be seen: satellite above the horizon and lit, observer in a dark sky.
/// It is often shorter than the geometric pass, typically because the satellite enters the Earth's shadow mid-sky.
/// </summary>
public sealed record VisiblePass(SatellitePass Pass, PassPoint VisibleStart, PassPoint VisibleMax, PassPoint VisibleEnd)
{
    public TimeSpan VisibleDuration => VisibleEnd.Instant - VisibleStart.Instant;

    /// <summary>True when the satellite goes dark before setting: it will seem to vanish in mid-sky.</summary>
    public bool EndsInShadow => VisibleEnd.Instant < Pass.End.Instant;

    /// <summary>True when the satellite is already up when it emerges from shadow: it will seem to appear in mid-sky.</summary>
    public bool StartsFromShadow => VisibleStart.Instant > Pass.Start.Instant;
}
