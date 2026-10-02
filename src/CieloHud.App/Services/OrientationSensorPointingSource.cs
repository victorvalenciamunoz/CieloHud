using CieloHud.Core.Guidance;

namespace CieloHud.App.Services;

/// <summary>
/// <see cref="IPointingSource"/> over MAUI's <see cref="OrientationSensor"/> (Android rotation vector, magnetic north).
/// Converts the quaternion with <see cref="OrientationMath"/>, applies the magnetic declination and smooths the result.
/// </summary>
public sealed class OrientationSensorPointingSource : IPointingSource
{
    private readonly PointingSmoother _smoother = new(alpha: 0.2);

    public event EventHandler<PointingReading>? ReadingChanged;

    public bool IsSupported => OrientationSensor.Default.IsSupported;
    public bool IsRunning { get; private set; }
    public PointingReading? Last { get; private set; }
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
        var q = e.Reading.Orientation;
        var magnetic = OrientationMath.ToPointing(q);
        var raw = new PointingDirection(magnetic.AzimuthDegrees + DeclinationDegrees, magnetic.AltitudeDegrees);
        var smoothed = _smoother.Push(raw);
        var roll = OrientationMath.RollDegrees(q);

        Last = new PointingReading(smoothed, roll, raw, DateTimeOffset.UtcNow);
        ReadingChanged?.Invoke(this, Last);
    }
}
