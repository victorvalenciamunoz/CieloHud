namespace CieloHud.Core.Guidance;

/// <summary>
/// Exponential smoothing of a pointing direction. Azimuth is smoothed on the circle, so going from 350° to 10°
/// passes through 0°, not through 180°. Not thread-safe; feed it from one sensor callback.
/// </summary>
public sealed class PointingSmoother
{
    private readonly double _alpha;
    private PointingDirection? _current;

    /// <param name="alpha">Weight of each new reading in (0, 1]: 1 = no smoothing, small = slow and steady.</param>
    public PointingSmoother(double alpha = 0.2)
    {
        if (double.IsNaN(alpha) || alpha <= 0 || alpha > 1)
            throw new ArgumentOutOfRangeException(nameof(alpha), alpha, "Alpha must be in (0, 1].");
        _alpha = alpha;
    }

    public PointingDirection? Current => _current;

    public PointingDirection Push(PointingDirection reading)
    {
        if (_current is not { } previous)
        {
            _current = reading;
            return reading;
        }

        var azimuthStep = GuidanceCalculator.WrapToHalfTurn(reading.AzimuthDegrees - previous.AzimuthDegrees);
        var azimuth = previous.AzimuthDegrees + azimuthStep * _alpha;
        var altitude = previous.AltitudeDegrees + (reading.AltitudeDegrees - previous.AltitudeDegrees) * _alpha;

        _current = new PointingDirection(azimuth, altitude);
        return _current.Value;
    }

    public void Reset() => _current = null;
}
