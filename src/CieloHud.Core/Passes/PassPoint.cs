using CieloHud.Core.Sky;

namespace CieloHud.Core.Passes;

/// <summary>Where the satellite is at one moment of a pass.</summary>
public readonly record struct PassPoint(DateTimeOffset Instant, HorizontalPosition Position);
