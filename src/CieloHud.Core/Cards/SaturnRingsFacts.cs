namespace CieloHud.Core.Cards;

public enum RingTrend
{
    /// <summary>Opening until their widest, about every 15 years.</summary>
    Opening,

    /// <summary>Closing until they are edge-on, about every 15 years.</summary>
    Closing,
}

/// <summary>Saturn's rings right now, for its card (decision 040).</summary>
/// <param name="TiltDegrees">
/// The rings' tilt as seen from the Earth, the latitude of the Earth over the ring plane: 0 edge-on, positive when the north face
/// shows (IAU and JPL Horizons convention; Astronomy Engine's <c>ring_tilt</c> has the opposite sign).
/// </param>
/// <param name="Trend">The long trend, set by Saturn's place around the Sun; month to month the Earth's own orbit can reverse it.</param>
/// <param name="Until">When the trend ends: the rings at their widest, or edge-on.</param>
/// <param name="TiltAtUntilDegrees">How tilted they will be then, as seen from the Sun (0 when edge-on).</param>
public sealed record SaturnRingsFacts(double TiltDegrees, RingTrend Trend, DateTimeOffset Until, double TiltAtUntilDegrees);
