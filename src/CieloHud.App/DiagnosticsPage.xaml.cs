using System.Globalization;
using CieloHud.App.Alerts;
using CieloHud.App.Hud;
using CieloHud.App.Services;
using CieloHud.Core.Guidance;
using CieloHud.Core.Sky;
using CieloHud.Core.SolarSystem;

namespace CieloHud.App;

/// <summary>
/// Numbers only: where we are, where the phone points, where the Sun and Moon are. First real-sky check of the pipeline.
/// </summary>
public partial class DiagnosticsPage : ContentPage
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly TimeSpan UiInterval = TimeSpan.FromMilliseconds(100);

    private readonly IPointingSource _pointing;
    private readonly ILocationSource _location;
    private readonly ISolarSystemService _solarSystem;
    private readonly ISunService _sun;
    private readonly NightMode _nightMode;
    private readonly PassAlertService _alerts;
    private readonly GuidanceCalculator _guidance = new();

    private Observer? _observer;
    private HorizontalPosition _sunPosition;
    private HorizontalPosition _moonPosition;
    private bool _onSun, _onMoon;
    private DateTimeOffset _lastUiUpdate;
    private IDispatcherTimer? _skyTimer;

    public DiagnosticsPage(IPointingSource pointing, ILocationSource location, ISolarSystemService solarSystem, ISunService sun, NightMode nightMode,
        PassAlertService alerts)
    {
        InitializeComponent();
        _pointing = pointing;
        _location = location;
        _solarSystem = solarSystem;
        _sun = sun;
        _nightMode = nightMode;
        _alerts = alerts;
#if DEBUG
        TestAlertButtons.IsVisible = true;
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_pointing.IsSupported)
            StatusLabel.Text = "Este móvil no tiene sensor de orientación.";
        _pointing.ReadingChanged += OnPointingChanged;
        _pointing.Start();

        _skyTimer = Dispatcher.CreateTimer();
        _skyTimer.Interval = TimeSpan.FromSeconds(1);
        _skyTimer.Tick += (_, _) =>
        {
            UpdateSky();
            UpdateAlerts();
        };
        _skyTimer.Start();

        UpdateAlerts();
        await RefreshLocationAsync();
    }

    protected override void OnDisappearing()
    {
        _pointing.ReadingChanged -= OnPointingChanged;
        _pointing.Stop();
        _skyTimer?.Stop();
        base.OnDisappearing();
    }

    private async Task RefreshLocationAsync()
    {
        var fix = await _location.GetAsync();
        if (fix is null)
        {
            LocationLabel.Text = "sin ubicación";
            StatusLabel.Text = "Sin permiso de ubicación o sin GPS.";
            return;
        }

        _observer = fix.Observer;
        var o = fix.Observer;
        LocationLabel.Text = $"{o.LatitudeDegrees.ToString("F4", Culture)}, {o.LongitudeDegrees.ToString("F4", Culture)}  {o.AltitudeMeters.ToString("F0", Culture)} m";
        AccuracyLabel.Text = fix.AccuracyMeters is { } acc ? $"±{acc.ToString("F0", Culture)} m" : "–";

        var declination = MagneticDeclination.Degrees(o, DateTimeOffset.UtcNow);
        _pointing.DeclinationDegrees = declination;
        DeclinationLabel.Text = $"{declination.ToString("+0.0;-0.0", Culture)}°";

        UpdateSky();
    }

    private void UpdateSky()
    {
        if (_observer is not { } observer)
            return;

        var now = DateTimeOffset.UtcNow;
        _sunPosition = _sun.Locate(observer, now);
        _moonPosition = _solarSystem.Locate(CelestialBody.Moon, observer, now);

        SunLabel.Text = Position(_sunPosition);
        MoonLabel.Text = Position(_moonPosition);
        UpdateGuidance();
    }

    private void OnPointingChanged(object? sender, PointingReading reading)
    {
        if (reading.Timestamp - _lastUiUpdate < UiInterval)
            return;
        _lastUiUpdate = reading.Timestamp;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            AccuracyLabel2.Text = _pointing.Accuracy switch
            {
                PointingAccuracy.High => "alta",
                PointingAccuracy.Medium => "media",
                PointingAccuracy.Low => "BAJA · haz un 8 con el móvil",
                PointingAccuracy.Unreliable => "NO FIABLE · haz un 8 con el móvil",
                _ => "desconocida",
            };
            AccuracyLabel2.TextColor = _pointing.Accuracy.NeedsCalibration() ? _nightMode.Palette.Alert : _nightMode.Palette.Text;
            AzimuthLabel.Text = $"{reading.Pointing.AzimuthDegrees.ToString("F1", Culture)}°  {Cardinal(reading.Pointing.CardinalPoint())}";
            AltitudeLabel.Text = $"{reading.Pointing.AltitudeDegrees.ToString("F1", Culture)}°";
            RollLabel.Text = $"{reading.RollDegrees.ToString("F0", Culture)}°";
            RawLabel.Text = $"{reading.RawPointing.AzimuthDegrees.ToString("F1", Culture)}° / {reading.RawPointing.AltitudeDegrees.ToString("F1", Culture)}°";
            UpdateGuidance();
        });
    }

    private void UpdateGuidance()
    {
        if (_observer is null || _pointing.Last is not { } reading)
            return;

        var sun = _guidance.Compute(reading.Pointing, _sunPosition, _onSun);
        _onSun = sun.IsOnTarget;
        SunGuidanceLabel.Text = Hint(sun);

        var moon = _guidance.Compute(reading.Pointing, _moonPosition, _onMoon);
        _onMoon = moon.IsOnTarget;
        MoonGuidanceLabel.Text = Hint(moon);
    }

    /// <summary>What is armed, so it can be checked without waiting for a pass (and compared with Heavens-Above).</summary>
    private void UpdateAlerts()
    {
        AlertsStateLabel.Text = _alerts.IsOn ? "activados" : "desactivados";
        var platform = _alerts.Platform;
        AlertsPermissionsLabel.Text = $"notificaciones {YesNo(platform.NotificationsAllowed)} · alarmas exactas {YesNo(platform.ExactAlarmsAllowed)}";
        AlertsPlannedLabel.Text = _alerts.PlannedAt is { } planned
            ? $"{LocalTime(planned)} · {_alerts.PlanningDays} días" + (_alerts.Problem is { } problem ? $"\n{problem}" : "")
            : "nunca";

        var pending = _alerts.IsOn ? _alerts.Pending : [];
        AlertsNextLabel.Text = pending.FirstOrDefault() is { } next ? $"{LocalTime(next.NotifyAt)}\n{next.Title}\n{next.Body}" : "–";
        AlertsLaterLabel.Text = pending.Count > 1
            ? string.Join("\n", pending.Skip(1).Select(a => $"{LocalTime(a.NotifyAt)} · {a.Body}"))
            : "–";
        AlertsLastWakeLabel.Text = _alerts.LastWake is var (fired, target)
            ? $"{LocalTime(fired, "HH:mm:ss")} · objetivo {LocalTime(target, "HH:mm:ss")} ({(fired - target).TotalSeconds.ToString("+0;-0", Culture)} s)"
            : "–";
    }

    private void OnTestAlertClicked(object? sender, EventArgs e)
    {
        var seconds = int.Parse((string)((Button)sender!).CommandParameter, Culture);
        _alerts.ArmTest(TimeSpan.FromSeconds(seconds));
        StatusLabel.Text = $"Aviso de prueba a las {LocalTime(DateTimeOffset.UtcNow.AddSeconds(seconds), "HH:mm:ss")}: puedes cerrar la app.";
    }

    private static string YesNo(bool value) => value ? "sí" : "NO";

    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    private static string LocalTime(DateTimeOffset instant, string format = "ddd d HH:mm") =>
        TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.Local).ToString(format, Spanish);

    private static string Position(HorizontalPosition p) =>
        $"{p.AzimuthDegrees.ToString("F1", Culture)}° {Cardinal(p.CardinalPoint)}  alt {p.AltitudeDegrees.ToString("F1", Culture)}°";

    private static string Hint(Guidance g)
    {
        if (g.IsOnTarget)
            return $"¡AQUÍ!  ({g.AngularDistanceDegrees.ToString("F1", Culture)}°)";
        var turn = g.AzimuthDeltaDegrees >= 0 ? "derecha" : "izquierda";
        var tilt = g.AltitudeDeltaDegrees >= 0 ? "sube" : "baja";
        return $"{turn} {Math.Abs(g.AzimuthDeltaDegrees).ToString("F0", Culture)}°, {tilt} {Math.Abs(g.AltitudeDeltaDegrees).ToString("F0", Culture)}°";
    }

    private static string Cardinal(CardinalPoint point) => point switch
    {
        CardinalPoint.SW => "SO",
        CardinalPoint.W => "O",
        CardinalPoint.NW => "NO",
        _ => point.ToString(),
    };
}

internal static class PointingExtensions
{
    public static CardinalPoint CardinalPoint(this PointingDirection p) => Azimuth.ToCardinalPoint(p.AzimuthDegrees);
}
