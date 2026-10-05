using System.Globalization;
using CieloHud.App.Alerts;
using CieloHud.App.Hud;
using CieloHud.App.Services;
using CieloHud.Core.Constellations;
using CieloHud.Core.Guidance;
using CieloHud.Core.Sky;
using CieloHud.Core.Stars;
using Guidance = CieloHud.Core.Guidance.Guidance;

namespace CieloHud.App;

/// <summary>
/// The guide: pick a target and follow the marker, or pick "¿QUÉ ES?" and point at something to name it.
/// Sensors feed <see cref="HudFrame"/>s to the drawable.
/// </summary>
public partial class HudPage : ContentPage
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(33);

    private readonly IPointingSource _pointing;
    private readonly ILocationSource _location;
    private readonly TargetCatalog _catalog;
    private readonly IConstellationLocator _constellations;
    private readonly IConstellationFigureLocator _figures;
    private readonly NightMode _nightMode;
    private readonly PassAlertService _alerts;
    private readonly ObserverStore _observers;
    private readonly GuidanceCalculator _guidance = new();
    private readonly HudDrawable _drawable = new();
    private readonly Dictionary<SkyTarget, Button> _chips = new();
    private Button _identifyChip = null!;

    /// <summary>Selected target; null means identify mode.</summary>
    private SkyTarget? _target;
    private Observer? _observer;
    private bool _onTarget;
    private IDispatcherTimer? _frameTimer;
    private readonly DateTimeOffset _started = DateTimeOffset.UtcNow;

    // Sky positions move slowly; recompute once a second, not every frame.
    private DateTimeOffset _skyComputedAt = DateTimeOffset.MinValue;
    private IReadOnlyList<(SkyTarget Target, HorizontalPosition Position)> _sky = [];
    private string? _targetConstellation;

    // Figure of the constellation under the reticle: recomputed when the reticle moves to another one, or once a second.
    private string? _figureSymbol;
    private DateTimeOffset _figureComputedAt = DateTimeOffset.MinValue;
    private IReadOnlyList<IReadOnlyList<HorizontalPosition>> _figure = [];

    public HudPage(IPointingSource pointing, ILocationSource location, TargetCatalog catalog,
        IConstellationLocator constellations, IConstellationFigureLocator figures, NightMode nightMode,
        PassAlertService alerts, ObserverStore observers)
    {
        InitializeComponent();
        _pointing = pointing;
        _location = location;
        _catalog = catalog;
        _constellations = constellations;
        _figures = figures;
        _nightMode = nightMode;
        _alerts = alerts;
        _observers = observers;
        _target = catalog.Targets[0];
        Canvas.Drawable = _drawable;
        BuildTargetBar();
        // In the constructor, not OnAppearing: a tap on an alert must reach the HUD also while diagnostics is on top.
        LaunchRequests.Requested += OnLaunchRequested;
        ApplyPalette();
    }

    private void BuildTargetBar()
    {
        _identifyChip = CreateChip("¿QUÉ ES?");
        _identifyChip.Clicked += (_, _) => SelectTarget(null);
        TargetBar.Children.Add(_identifyChip);

        foreach (var target in _catalog.Targets)
        {
            var chip = CreateChip(target.Name.ToUpperInvariant());
            chip.Clicked += (_, _) => SelectTarget(target);
            _chips[target] = chip;
            TargetBar.Children.Add(chip);
        }
        StyleChips();
    }

    private static Button CreateChip(string text) => new()
    {
        Text = text,
        FontFamily = "ChakraPetchBold",
        FontSize = 12,
        CornerRadius = 14,
        Padding = new Thickness(14, 6),
        BorderWidth = 1,
    };

    private void StyleChips()
    {
        StyleChip(_identifyChip, _target is null);
        foreach (var (target, chip) in _chips)
            StyleChip(chip, target == _target);
    }

    private void StyleChip(Button chip, bool selected)
    {
        var p = _nightMode.Palette;
        chip.BackgroundColor = selected ? p.LockedFill : Colors.Transparent;
        chip.TextColor = selected ? p.Locked : p.TextMuted;
        chip.BorderColor = selected ? p.Locked : p.TextDim;
    }

    /// <summary>Colors that are not <c>DynamicResource</c>s: the drawable, the chips and the night button.</summary>
    private void ApplyPalette()
    {
        _drawable.Palette = _nightMode.Palette;
        StyleChips();
        StyleChip(NightButton, _nightMode.IsOn);
        StyleChip(AlertsButton, _alerts.IsOn);
    }

    private void OnNightClicked(object? sender, EventArgs e)
    {
        _nightMode.Toggle();
        ApplyPalette();
    }

    /// <summary>
    /// Off by default (not invasive): the first tap asks for the notification permission and, if exact alarms are not
    /// allowed, offers the system screen for them. Then it says what the next alert is.
    /// </summary>
    private async void OnAlertsClicked(object? sender, EventArgs e)
    {
        if (_alerts.IsOn)
        {
            _alerts.TurnOff();
            ApplyPalette();
            return;
        }

        var asksPermission = !_alerts.Platform.NotificationsAllowed;
        if (!await _alerts.TurnOnAsync())
        {
            await DisplayAlertAsync("AVISOS", "Sin permiso de notificaciones no se puede avisar. Puedes darlo en los ajustes de la app.", "Vale");
            return;
        }
        ApplyPalette();
        // Seen on the OPPO: a dialog shown while the system permission dialog is still closing is cancelled at once,
        // which reads as "Ahora no". Let the activity come back to the front first.
        if (asksPermission)
            await Task.Delay(TimeSpan.FromMilliseconds(600));

        if (!_alerts.Platform.ExactAlarmsAllowed
            && await DisplayAlertAsync("AVISOS", "Para avisar a la hora exacta, permite «Alarmas y recordatorios» para CieloHud. Sin ese permiso el aviso puede llegar unos minutos tarde.", "Abrir ajustes", "Ahora no"))
        {
            // Back from the settings, the window activation re-arms the alarm as exact.
            _alerts.Platform.OpenExactAlarmSettings();
            return;
        }

        await DisplayAlertAsync("AVISOS", AlertsSummary(), "Vale");
    }

    private string AlertsSummary()
    {
        if (_alerts.Problem is { } problem)
            return $"Avisos activados, pero ahora mismo no se pueden calcular: {problem}.";
        if (_alerts.Pending.FirstOrDefault() is not { } next)
            return $"Avisos activados. La ISS no tiene pasos visibles en los próximos {_alerts.PlanningDays} días; se vuelve a mirar cada vez que abres la app.";
        var at = TimeZoneInfo.ConvertTime(next.NotifyAt, TimeZoneInfo.Local);
        return $"Avisos activados. Próximo aviso: {at.ToString("ddd d HH:mm", SpanishCulture)}\n\n{next.Body}";
    }

    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>An alert was tapped: guide to what it announced.</summary>
    private void OnLaunchRequested(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(async () =>
    {
        // From another page (diagnostics), back to the HUD first; its OnAppearing takes the request.
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopToRootAsync(false);
        else
            TakeLaunchRequest();
    });

    private async void TakeLaunchRequest()
    {
        if (LaunchRequests.Take() is not { } name || _catalog.Targets.FirstOrDefault(t => t.Name == name) is not { } target)
            return;
        SelectTarget(target);
        // The ISS chip is the last one, off screen on most phones: show which target the HUD is guiding to.
        await TargetScroll.ScrollToAsync(_chips[target], ScrollToPosition.MakeVisible, false);
    }

    private async void SelectTarget(SkyTarget? target)
    {
        _target = target;
        _onTarget = false;
        _skyComputedAt = DateTimeOffset.MinValue;
        StyleChips();
        await PrepareSatellitesAsync();
    }

    /// <summary>The ISS needs its orbit downloaded; do it when it is the target or could be identified.</summary>
    private async Task PrepareSatellitesAsync()
    {
        foreach (var satellite in _catalog.Targets.OfType<SatelliteTarget>())
        {
            if (_target is null || _target == satellite)
                await satellite.PrepareAsync();
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        TakeLaunchRequest();
        _pointing.Start();

        _frameTimer = Dispatcher.CreateTimer();
        _frameTimer.Interval = FrameInterval;
        _frameTimer.Tick += (_, _) => RenderFrame();
        _frameTimer.Start();

        var fix = await _location.GetAsync();
        if (fix is { } f)
        {
            _observer = f.Observer;
            // For the alerts, planned in the background without reading the location.
            _observers.Save(f.Observer);
            if (_alerts.IsOn)
                _ = Task.Run(() => _alerts.RescheduleAsync("nueva ubicación"));
            _pointing.DeclinationDegrees = MagneticDeclination.Degrees(f.Observer, DateTimeOffset.UtcNow);
            FooterLabel.Text = $"{f.Observer.LatitudeDegrees.ToString("F3", Culture)}, {f.Observer.LongitudeDegrees.ToString("F3", Culture)}  ·  decl {_pointing.DeclinationDegrees.ToString("+0.0;-0.0", Culture)}°";
        }
        else
        {
            FooterLabel.Text = "Sin ubicación: activa el GPS y da permiso";
        }

        await PrepareSatellitesAsync();
    }

    protected override void OnDisappearing()
    {
        _frameTimer?.Stop();
        _pointing.Stop();
        base.OnDisappearing();
    }

    private void RenderFrame()
    {
        var now = DateTimeOffset.UtcNow;
        var pointing = _pointing.Last?.Pointing;
        UpdateSky(now);
        var underReticle = pointing is { } aim && _observer is { } observer
            ? _constellations.Locate(aim.AzimuthDegrees, aim.AltitudeDegrees, observer, now)
            : null;
        UpdateFigure(underReticle?.Symbol, now);

        HorizontalPosition? targetPosition = _target is null ? null : _sky.FirstOrDefault(s => s.Target == _target) is { Target: not null } hit ? hit.Position : null;

        Guidance? guidance = null;
        if (pointing is { } p && targetPosition is { } t)
        {
            var g = _guidance.Compute(p, t, _onTarget);
            _onTarget = g.IsOnTarget;
            guidance = g;
        }

        _drawable.Frame = new HudFrame
        {
            TargetName = _target?.Name ?? "",
            Target = targetPosition,
            Unavailable = _target?.Unavailable,
            Pointing = pointing,
            Guidance = guidance,
            HasLocation = _observer is not null,
            Pulse = (now - _started).TotalSeconds % 1.0,
            References = _sky
                .Where(s => s.Target != _target)
                .Select(s => new ReferenceObject(s.Target.Name, s.Position, (s.Target as StarTarget)?.Star.Magnitude))
                .ToList(),
            NeedsCalibration = _pointing.Accuracy.NeedsCalibration(),
            IdentifyMode = _target is null,
            Identified = _target is null && pointing is { } here ? Identify(here, now) : null,
            PointingConstellation = underReticle is { } c ? SpanishNames.Constellation(c) : null,
            TargetConstellation = _targetConstellation,
            ConstellationFigure = _figure,
            ConstellationFigureName = underReticle is { } n ? SpanishNames.WithoutArticle(SpanishNames.Constellation(n)) : null,
        };
        Canvas.Invalidate();
    }

    private void UpdateFigure(string? symbol, DateTimeOffset now)
    {
        if (symbol is null || _observer is not { } observer)
        {
            _figure = [];
            _figureSymbol = null;
            return;
        }
        if (symbol == _figureSymbol && now - _figureComputedAt < TimeSpan.FromSeconds(1))
            return;

        _figure = _figures.Locate(symbol, observer, now);
        _figureSymbol = symbol;
        _figureComputedAt = now;
    }

    private IdentifyResult? Identify(PointingDirection pointing, DateTimeOffset now)
    {
        var candidates = _sky.Select(s => new SkyCandidate(s.Target.Name, s.Position, (s.Target as StarTarget)?.Star.Magnitude)).ToList();
        var match = SkyIdentifier.Identify(pointing, candidates);
        var found = match ?? SkyIdentifier.Nearest(pointing, candidates);
        if (found is not { } f)
            return null;

        var kind = _sky.First(s => s.Target.Name == f.Candidate.Name).Target.Kind;
        var constellation = ConstellationAt(f.Candidate.Position.AzimuthDegrees, f.Candidate.Position.AltitudeDegrees, now) ?? "";
        return new IdentifyResult(f.Candidate.Name, kind, f.Candidate.Position, f.AngularDistanceDegrees, IsMatch: match is not null, constellation);
    }

    /// <summary>Spanish constellation name with article for a direction, or null without a location fix.</summary>
    private string? ConstellationAt(double azimuthDegrees, double altitudeDegrees, DateTimeOffset now) =>
        _observer is { } o ? SpanishNames.Constellation(_constellations.Locate(azimuthDegrees, altitudeDegrees, o, now)) : null;

    private void UpdateSky(DateTimeOffset now)
    {
        if (_observer is not { } observer)
            return;
        if (now - _skyComputedAt < TimeSpan.FromSeconds(1))
            return;
        _skyComputedAt = now;

        _sky = _catalog.All
            .Select(t => (Target: t, Position: t.Locate(observer, now)))
            .Where(x => x.Position is not null)
            .Select(x => (x.Target, x.Position!.Value))
            .ToList();

        _targetConstellation = _target is not null && _sky.FirstOrDefault(s => s.Target == _target) is { Target: not null } t
            ? ConstellationAt(t.Position.AzimuthDegrees, t.Position.AltitudeDegrees, now)
            : null;
    }

    private async void OnDiagnosticsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(DiagnosticsPage));
    }
}
