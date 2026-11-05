namespace CieloHud.Core.Passes;

/// <summary>
/// What counts as a visible pass. Defaults follow docs/PLAN.md and Heavens-Above: observer in at least civil twilight,
/// satellite lit, and high enough to clear buildings and haze.
/// </summary>
public sealed record VisibilityCriteria
{
    /// <summary>
    /// The Sun must be below this geometric altitude when the satellite becomes visible. Civil twilight: -6°.
    /// The sky may brighten past it during the pass without ending the visible part.
    /// </summary>
    public double MaxSunAltitudeDegrees { get; init; } = -6;

    /// <summary>The visible part of the pass must reach at least this altitude.</summary>
    public double MinPeakAltitudeDegrees { get; init; } = 10;

    /// <summary>Sampling step along the pass. Visible start/end are resolved to this precision.</summary>
    public TimeSpan Step { get; init; } = TimeSpan.FromSeconds(10);

    public static VisibilityCriteria Default { get; } = new();
}
