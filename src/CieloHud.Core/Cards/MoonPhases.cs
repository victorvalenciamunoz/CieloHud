namespace CieloHud.Core.Cards;

/// <summary>
/// Names the Moon's phase as people do (decision 036): a principal phase is named within <see cref="PrincipalPhaseSpan"/>
/// of its exact instant, when the eye cannot tell 98 % from 100 % lit or a half Moon from a slightly fuller one; between
/// them, crescent or gibbous, waxing or waning.
/// </summary>
public static class MoonPhases
{
    public static readonly TimeSpan PrincipalPhaseSpan = TimeSpan.FromDays(1);

    /// <param name="phaseDegrees">Moon–Sun geocentric ecliptic longitude difference, 0 to 360.</param>
    /// <param name="previous">The last principal phase at or before <paramref name="instant"/>.</param>
    /// <param name="next">The first principal phase after <paramref name="instant"/>.</param>
    public static MoonPhaseName Name(double phaseDegrees, MoonQuarter previous, MoonQuarter next, DateTimeOffset instant)
    {
        if (phaseDegrees is < 0 or >= 360 || double.IsNaN(phaseDegrees))
            throw new ArgumentOutOfRangeException(nameof(phaseDegrees), phaseDegrees, "Phase must be in [0, 360).");

        var toPrevious = instant - previous.Instant;
        var toNext = next.Instant - instant;
        if (toPrevious <= PrincipalPhaseSpan || toNext <= PrincipalPhaseSpan)
            return Principal(toPrevious <= toNext ? previous.Kind : next.Kind);

        return phaseDegrees switch
        {
            < 90 => MoonPhaseName.WaxingCrescent,
            < 180 => MoonPhaseName.WaxingGibbous,
            < 270 => MoonPhaseName.WaningGibbous,
            _ => MoonPhaseName.WaningCrescent,
        };
    }

    private static MoonPhaseName Principal(MoonQuarterKind kind) => kind switch
    {
        MoonQuarterKind.NewMoon => MoonPhaseName.NewMoon,
        MoonQuarterKind.FirstQuarter => MoonPhaseName.FirstQuarter,
        MoonQuarterKind.FullMoon => MoonPhaseName.FullMoon,
        MoonQuarterKind.LastQuarter => MoonPhaseName.LastQuarter,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
