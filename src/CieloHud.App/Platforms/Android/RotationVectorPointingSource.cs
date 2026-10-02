using Android.Content;
using Android.Hardware;
using Android.Runtime;
using CieloHud.App.Services;
using CieloHud.Core.Guidance;

namespace CieloHud.App.Platforms.Android;

/// <summary>
/// <see cref="IPointingSource"/> on Android's rotation-vector sensor through <see cref="SensorManager"/> directly,
/// which (unlike MAUI's wrapper) reports the sensor accuracy so we can ask for a figure-8 calibration.
/// </summary>
public sealed class RotationVectorPointingSource : Java.Lang.Object, IPointingSource, ISensorEventListener
{
    private readonly PointingSmoother _smoother = new(alpha: PointingSmoothing.Alpha);
    private readonly SensorManager? _manager;
    private readonly Sensor? _sensor;
    private readonly float[] _quaternion = new float[4];

    public RotationVectorPointingSource()
    {
        _manager = global::Android.App.Application.Context.GetSystemService(Context.SensorService) as SensorManager;
        _sensor = _manager?.GetDefaultSensor(SensorType.RotationVector);
    }

    public event EventHandler<PointingReading>? ReadingChanged;
    public event EventHandler<PointingAccuracy>? AccuracyChanged;

    public bool IsSupported => _sensor is not null;
    public bool IsRunning { get; private set; }
    public PointingReading? Last { get; private set; }
    public PointingAccuracy Accuracy { get; private set; } = PointingAccuracy.Unknown;
    public double DeclinationDegrees { get; set; }

    public void Start()
    {
        if (IsRunning || _manager is null || _sensor is null)
            return;
        _smoother.Reset();
        _manager.RegisterListener(this, _sensor, SensorDelay.Game);
        IsRunning = true;
    }

    public void Stop()
    {
        if (!IsRunning)
            return;
        _manager?.UnregisterListener(this, _sensor);
        IsRunning = false;
    }

    public void OnSensorChanged(SensorEvent? e)
    {
        if (e?.Values is null || e.Values.Count < 3)
            return;

        // Q comes back as [w, x, y, z]; the Android frame conventions match OrientationMath.
        SensorManager.GetQuaternionFromVector(_quaternion, e.Values.ToArray());
        var q = new System.Numerics.Quaternion(_quaternion[1], _quaternion[2], _quaternion[3], _quaternion[0]);

        Last = PointingSmoothing.Convert(q, DeclinationDegrees, _smoother);
        ReadingChanged?.Invoke(this, Last);
        UpdateAccuracy(e.Accuracy);
    }

    public void OnAccuracyChanged(Sensor? sensor, [GeneratedEnum] SensorStatus accuracy) => UpdateAccuracy(accuracy);

    private void UpdateAccuracy(SensorStatus status)
    {
        var accuracy = status switch
        {
            SensorStatus.Unreliable => PointingAccuracy.Unreliable,
            SensorStatus.AccuracyLow => PointingAccuracy.Low,
            SensorStatus.AccuracyMedium => PointingAccuracy.Medium,
            SensorStatus.AccuracyHigh => PointingAccuracy.High,
            _ => PointingAccuracy.Unknown,
        };
        if (accuracy == Accuracy)
            return;
        Accuracy = accuracy;
        AccuracyChanged?.Invoke(this, accuracy);
    }
}
