using CieloHud.Core.Guidance;
using CieloHud.Core.Sky;

namespace CieloHud.App.Hud;

/// <summary>Another object worth drawing as a faint reference.</summary>
public sealed record ReferenceObject(string Name, HorizontalPosition Position);

/// <summary>Everything the drawable needs for one frame. Built on the UI thread, read by Draw.</summary>
public sealed record HudFrame
{
    public string TargetName { get; init; } = "";
    public HorizontalPosition? Target { get; init; }
    public string? Unavailable { get; init; }
    public PointingDirection? Pointing { get; init; }
    public Guidance? Guidance { get; init; }
    public bool HasLocation { get; init; }
    public double Pulse { get; init; } // 0..1, loops once per second
    public IReadOnlyList<ReferenceObject> References { get; init; } = [];
    public bool NeedsCalibration { get; init; }

    public bool TargetBelowHorizon => Target is { AltitudeDegrees: < 0 };
    public bool Ready => HasLocation && Pointing is not null && Target is not null && Guidance is not null;
}
