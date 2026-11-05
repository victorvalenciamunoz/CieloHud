namespace CieloHud.Core.Passes;

/// <summary>
/// One crossing of the observer's sky: from rising above the horizon to setting below it.
/// Pure geometry; says nothing about whether the satellite is lit or the sky is dark.
/// </summary>
public sealed record SatellitePass(PassPoint Start, PassPoint Max, PassPoint End)
{
    public TimeSpan Duration => End.Instant - Start.Instant;

    public double MaxAltitudeDegrees => Max.Position.AltitudeDegrees;
}
