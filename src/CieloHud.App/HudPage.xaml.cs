using System.Globalization;
using CieloHud.App.Hud;
using CieloHud.App.Services;
using CieloHud.Core.Guidance;
using CieloHud.Core.Sky;
using Guidance = CieloHud.Core.Guidance.Guidance;

namespace CieloHud.App;

/// <summary>
/// The guide: pick a target, point the phone, follow the marker. Sensors feed <see cref="HudFrame"/>s to the drawable.
/// </summary>
public partial class HudPage : ContentPage
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(33);

    private readonly IPointingSource _pointing;
    private readonly ILocationSource _location;
    private readonly TargetCatalog _catalog;
    private readonly GuidanceCalculator _guidance = new();
    private readonly HudDrawable _drawable = new();
    private readonly Dictionary<SkyTarget, Button> _chips = new();

    private SkyTarget _target;
    private Observer? _observer;
    private bool _onTarget;
    private IDispatcherTimer? _frameTimer;
    private readonly DateTimeOffset _started = DateTimeOffset.UtcNow;

    // Sky positions move slowly; recompute once a second, not every frame.
    private DateTimeOffset _skyComputedAt = DateTimeOffset.MinValue;
    private HorizontalPosition? _targetPosition;
    private IReadOnlyList<ReferenceObject> _references = [];

    public HudPage(IPointingSource pointing, ILocationSource location, TargetCatalog catalog)
    {
        InitializeComponent();
        _pointing = pointing;
        _location = location;
        _catalog = catalog;
        _target = catalog.Targets[0];
        Canvas.Drawable = _drawable;
        BuildTargetBar();
    }

    private void BuildTargetBar()
    {
        foreach (var target in _catalog.Targets)
        {
            var chip = new Button
            {
                Text = target.Name.ToUpperInvariant(),
                FontFamily = "ChakraPetchBold",
                FontSize = 12,
                CornerRadius = 14,
                Padding = new Thickness(14, 6),
                BorderWidth = 1,
            };
            chip.Clicked += (_, _) => SelectTarget(target);
            _chips[target] = chip;
            TargetBar.Children.Add(chip);
        }
        StyleChips();
    }

    private void StyleChips()
    {
        foreach (var (target, chip) in _chips)
        {
            var selected = target == _target;
            chip.BackgroundColor = selected ? Color.FromArgb("#1F6F4A") : Colors.Transparent;
            chip.TextColor = selected ? Color.FromArgb("#7CFFB2") : Color.FromArgb("#8FA0B0");
            chip.BorderColor = selected ? Color.FromArgb("#7CFFB2") : Color.FromArgb("#5A6A7A");
        }
    }

    private async void SelectTarget(SkyTarget target)
    {
        _target = target;
        _onTarget = false;
        _skyComputedAt = DateTimeOffset.MinValue;
        StyleChips();
        if (target is SatelliteTarget satellite)
            await satellite.PrepareAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _pointing.Start();

        _frameTimer = Dispatcher.CreateTimer();
        _frameTimer.Interval = FrameInterval;
        _frameTimer.Tick += (_, _) => RenderFrame();
        _frameTimer.Start();

        var fix = await _location.GetAsync();
        if (fix is { } f)
        {
            _observer = f.Observer;
            _pointing.DeclinationDegrees = MagneticDeclination.Degrees(f.Observer, DateTimeOffset.UtcNow);
            FooterLabel.Text = $"{f.Observer.LatitudeDegrees.ToString("F3", Culture)}, {f.Observer.LongitudeDegrees.ToString("F3", Culture)}  ·  decl {_pointing.DeclinationDegrees.ToString("+0.0;-0.0", Culture)}°";
        }
        else
        {
            FooterLabel.Text = "Sin ubicación: activa el GPS y da permiso";
        }

        if (_target is SatelliteTarget satellite)
            await satellite.PrepareAsync();
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

        Guidance? guidance = null;
        if (pointing is { } p && _targetPosition is { } t)
        {
            var g = _guidance.Compute(p, t, _onTarget);
            _onTarget = g.IsOnTarget;
            guidance = g;
        }

        _drawable.Frame = new HudFrame
        {
            TargetName = _target.Name,
            Target = _targetPosition,
            Unavailable = _target.Unavailable,
            Pointing = pointing,
            Guidance = guidance,
            HasLocation = _observer is not null,
            Pulse = (now - _started).TotalSeconds % 1.0,
            References = _references,
            NeedsCalibration = _pointing.Accuracy.NeedsCalibration(),
        };
        Canvas.Invalidate();
    }

    private void UpdateSky(DateTimeOffset now)
    {
        if (_observer is not { } observer)
            return;
        if (now - _skyComputedAt < TimeSpan.FromSeconds(1))
            return;
        _skyComputedAt = now;

        _targetPosition = _target.Locate(observer, now);
        _references = _catalog.Targets
            .Where(t => t != _target)
            .Select(t => (t.Name, Position: t.Locate(observer, now)))
            .Where(x => x.Position is not null)
            .Select(x => new ReferenceObject(x.Name, x.Position!.Value))
            .ToList();
    }

    private async void OnDiagnosticsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(DiagnosticsPage));
    }
}
