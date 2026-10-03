using CieloHud.Core.Guidance;
using CieloHud.Core.Sky;

namespace CieloHud.App.Hud;

/// <summary>Another object worth drawing as a faint reference.</summary>
/// <param name="Magnitude">Brightness for stars (lower = brighter); null for Moon, planets and ISS.</param>
public sealed record ReferenceObject(string Name, HorizontalPosition Position, double? Magnitude = null);

/// <summary>What the reticle is on in identify mode, or the nearest thing when nothing is close.</summary>
/// <param name="Constellation">Spanish constellation of the object, with article ("la Ballena").</param>
public sealed record IdentifyResult(string Name, string Kind, HorizontalPosition Position, double AngularDistanceDegrees, bool IsMatch, string Constellation);

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

    /// <summary>True when the user asked "what is that?" instead of picking a target.</summary>
    public bool IdentifyMode { get; init; }
    public IdentifyResult? Identified { get; init; }

    /// <summary>Spanish constellation the reticle is on, with article ("Orión", "la Osa Mayor").</summary>
    public string? PointingConstellation { get; init; }

    /// <summary>Spanish constellation the selected target is in, with article.</summary>
    public string? TargetConstellation { get; init; }

    public bool TargetBelowHorizon => Target is { AltitudeDegrees: < 0 };
    public bool Ready => HasLocation && Pointing is not null && Target is not null && Guidance is not null;
}
