namespace CieloHud.Core.Guidance;

/// <summary>
/// How to move the device to reach the target.
/// </summary>
/// <param name="AzimuthDeltaDegrees">Turn needed around the vertical axis, in (-180, 180]: positive = turn right (clockwise).</param>
/// <param name="AltitudeDeltaDegrees">Tilt needed, in [-180, 180]: positive = raise the device.</param>
/// <param name="AngularDistanceDegrees">Great-circle angle between pointing and target, in [0, 180].</param>
/// <param name="IsOnTarget">True when the target is inside the target zone (with hysteresis).</param>
public readonly record struct Guidance(
    double AzimuthDeltaDegrees,
    double AltitudeDeltaDegrees,
    double AngularDistanceDegrees,
    bool IsOnTarget);
