namespace CieloHud.App.Alerts;

/// <summary>
/// A target asked for from outside the HUD (tapping an alert). The activity may receive it before the HUD exists, so it
/// is kept until the HUD takes it; <see cref="Requested"/> tells a HUD that is already open.
/// </summary>
public static class LaunchRequests
{
    /// <summary>Intent extra with the target name: "ISS" for a pass, "Luna" for a conjunction.</summary>
    public const string TargetExtra = "cielohud.target";

    private static string? _pending;

    public static event EventHandler? Requested;

    public static void Request(string target)
    {
        _pending = target;
        Requested?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>The pending target, once.</summary>
    public static string? Take()
    {
        var target = _pending;
        _pending = null;
        return target;
    }
}
