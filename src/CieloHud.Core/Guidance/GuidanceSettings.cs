namespace CieloHud.Core.Guidance;

/// <summary>
/// How close counts as "on target". Two radii give hysteresis so the state does not flicker with sensor noise:
/// you enter the target zone inside <see cref="EnterRadiusDegrees"/> and only leave it beyond <see cref="ExitRadiusDegrees"/>.
/// Phone magnetometers are good to a few degrees, so the zone is deliberately wide.
/// </summary>
public sealed record GuidanceSettings
{
    public double EnterRadiusDegrees { get; init; } = 4;
    public double ExitRadiusDegrees { get; init; } = 6;

    public static GuidanceSettings Default { get; } = new();
}
