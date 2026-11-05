using CieloHud.Core.Guidance;

namespace CieloHud.App.Services;

/// <summary>
/// <see cref="IPointingSource"/> over MAUI's cross-platform <see cref="OrientationSensor"/>. Portable fallback: it does not
/// report sensor accuracy, so <see cref="Accuracy"/> stays <see cref="PointingAccuracy.Unknown"/>.
/// On Android the app uses <c>RotationVectorPointingSource</c> instead, which does.
/// </summary>
public sealed class OrientationSensorPointingSource : IPointingSource
{
    private readonly PointingSmoother _smoother = new(alpha: PointingSmoothing.Alpha);

    public event EventHandler<PointingReading>? ReadingChanged;

    // Never raised: this source has no accuracy information.
    public event EventHandler<PointingAccuracy>? AccuracyChanged { add { } remove { } }

    public bool IsSupported => OrientationSensor.Default.IsSupported;
    public bool IsRunning { get; private set; }
    public PointingReading? Last { get; private set; }
    public PointingAccuracy Accuracy => PointingAccuracy.Unknown;
    public double DeclinationDegrees { get; set; }

    public void Start()
    {
        if (IsRunning || !IsSupported)
            return;
        _smoother.Reset();
        OrientationSensor.Default.ReadingChanged += OnReadingChanged;
        OrientationSensor.Default.Start(SensorSpeed.Game);
        IsRunning = true;
    }

    public void Stop()
    {
        if (!IsRunning)
            return;
        OrientationSensor.Default.ReadingChanged -= OnReadingChanged;
        if (OrientationSensor.Default.IsMonitoring)
            OrientationSensor.Default.Stop();
        IsRunning = false;
    }

    private void OnReadingChanged(object? sender, OrientationSensorChangedEventArgs e)
    {
        Last = PointingSmoothing.Convert(e.Reading.Orientation, DeclinationDegrees, _smoother);
        ReadingChanged?.Invoke(this, Last);
    }
}

/// <summary>Shared conversion so every source smooths the same way.</summary>
public static class PointingSmoothing
{
    /// <summary>At ~50 Hz this gives a response time of about half a second: steady in the hand, still responsive.</summary>
    public const double Alpha = 0.08;

    public static PointingReading Convert(System.Numerics.Quaternion deviceToWorld, double declinationDegrees, PointingSmoother smoother)
    {
        var magnetic = OrientationMath.ToPointing(deviceToWorld);
        var raw = new PointingDirection(magnetic.AzimuthDegrees + declinationDegrees, magnetic.AltitudeDegrees);
        var smoothed = smoother.Push(raw);
        var roll = OrientationMath.RollDegrees(deviceToWorld);
        return new PointingReading(smoothed, roll, raw, DateTimeOffset.UtcNow);
    }
}
