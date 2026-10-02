using CieloHud.Core.Guidance;

namespace CieloHud.App.Services;

/// <summary>One smoothed reading of where the back of the phone points.</summary>
/// <param name="Pointing">True-north azimuth and altitude.</param>
/// <param name="RollDegrees">Screen roll around the pointing axis.</param>
/// <param name="RawPointing">Same reading before smoothing, for diagnostics.</param>
public sealed record PointingReading(PointingDirection Pointing, double RollDegrees, PointingDirection RawPointing, DateTimeOffset Timestamp);

/// <summary>Streams the device pointing direction from the orientation sensor.</summary>
public interface IPointingSource
{
    event EventHandler<PointingReading>? ReadingChanged;

    bool IsSupported { get; }
    bool IsRunning { get; }
    PointingReading? Last { get; }

    /// <summary>Degrees added to the sensor's magnetic azimuth to get true north (east positive).</summary>
    double DeclinationDegrees { get; set; }

    void Start();
    void Stop();
}
