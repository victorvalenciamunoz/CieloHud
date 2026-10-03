using System.Globalization;
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

    public HudPage(IPointingSource pointing, ILocationSource location, TargetCatalog catalog, IConstellationLocator constellations)
    {
        InitializeComponent();
        _pointing = pointing;
        _location = location;
        _catalog = catalog;
        _constellations = constellations;
        _target = catalog.Targets[0];
        Canvas.Drawable = _drawable;
        BuildTargetBar();
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

    private static void StyleChip(Button chip, bool selected)
    {
        chip.BackgroundColor = selected ? Color.FromArgb("#1F6F4A") : Colors.Transparent;
        chip.TextColor = selected ? Color.FromArgb("#7CFFB2") : Color.FromArgb("#8FA0B0");
        chip.BorderColor = selected ? Color.FromArgb("#7CFFB2") : Color.FromArgb("#5A6A7A");
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
            PointingConstellation = _target is null && pointing is { } dir ? ConstellationAt(dir.AzimuthDegrees, dir.AltitudeDegrees, now) : null,
            TargetConstellation = _targetConstellation,
        };
        Canvas.Invalidate();
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
