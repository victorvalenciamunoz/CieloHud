namespace CieloHud.App.Alerts;

/// <summary>
/// A target asked for from outside the HUD (tapping an alert). The activity may receive it before the HUD exists, so it
/// is kept until the HUD takes it; <see cref="Listen"/> tells a HUD that is already open.
/// </summary>
public static class LaunchRequests
{
    /// <summary>Intent extra with the target name: "ISS" for a pass, "Luna" for a conjunction, "Mercurio"…</summary>
    public const string TargetExtra = "cielohud.target";

    private static string? _pending;
    private static Action? _listener;

    /// <summary>
    /// The HUD to tell about new requests. Only the last one listens: leaving with Back closes the activity but not the process, and
    /// opening the app again builds a new HUD. An event would still reach the old, hidden one, which would take the request first.
    /// </summary>
    public static void Listen(Action listener) => _listener = listener;

    /// <summary>A request for the HUD that is open: it is told now.</summary>
    public static void Request(string target)
    {
        _pending = target;
        _listener?.Invoke();
    }

    /// <summary>
    /// A request for a HUD about to be built (a new activity): kept for it to take when it appears. Telling the listener would
    /// reach the HUD of an activity already closed, which would take it.
    /// </summary>
    public static void Keep(string target) => _pending = target;

    /// <summary>The pending target, once.</summary>
    public static string? Take()
    {
        var target = _pending;
        _pending = null;
        return target;
    }
}
