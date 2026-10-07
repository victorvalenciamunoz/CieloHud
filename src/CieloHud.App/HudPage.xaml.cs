using System.Globalization;
using CieloHud.App.Alerts;
using CieloHud.App.Hud;
using CieloHud.App.Services;
using CieloHud.Core.Cards;
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
    private readonly AlertService _alerts;
    private readonly ObserverStore _observers;
    private readonly CardBuilder _cards;
    private readonly GuidanceCalculator _guidance = new();
    private readonly HudDrawable _drawable = new();
    private readonly CardDrawing _cardDrawing = new();
    private readonly Dictionary<SkyTarget, Button> _chips = new();
    private readonly Dictionary<float, Button> _brightnessChips = new();
    private Button _identifyChip = null!;

    /// <summary>Selected target; null means identify mode.</summary>
    private SkyTarget? _target;
    private Observer? _observer;
    private bool _onTarget;
    // No fix: the HUD says so instead of "searching".
    private bool _locationFailed;
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

    // The object card (decision 038): opened by itself on AQUÍ, or offered with VER FICHA in identify mode.
    private static readonly TimeSpan CardRefreshInterval = TimeSpan.FromSeconds(10);
    /// <summary>Whether VER FICHA is filled: in identify mode, and in guide mode on AQUÍ. Null until first styled.</summary>
    private bool? _cardButtonLit;
    private readonly RecentMatch<SkyTarget> _cardOffer = new();
    private SkyTarget? _offered;
    private SkyTarget? _cardTarget;
    private DateTimeOffset _cardBuiltAt;

    public HudPage(IPointingSource pointing, ILocationSource location, TargetCatalog catalog,
        IConstellationLocator constellations, IConstellationFigureLocator figures, NightMode nightMode,
        AlertService alerts, ObserverStore observers, CardBuilder cards)
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
        _cards = cards;
        _target = catalog.Targets[0];
        Canvas.Drawable = _drawable;
        CardPictureView.Drawable = _cardDrawing;
        BuildTargetBar();
        BuildBrightnessBar();
        // In the constructor, not OnAppearing: a tap on an alert must reach the HUD also while diagnostics is on top.
        LaunchRequests.Listen(OnLaunchRequested);
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
        _cardDrawing.Palette = _nightMode.Palette;
        CardPictureView.Invalidate();
        StyleChips();
        StyleChip(NightButton, _nightMode.IsOn);
        StyleChip(AlertsButton, _alerts.IsOn);
        StyleChip(LeaveNightButton, false);
        StyleChip(CloseCardButton, false);
        StyleChip(CardMoreButton, false);
        StyleChip(ShowCardButton, _cardButtonLit ?? true);
        foreach (var (brightness, chip) in _brightnessChips)
            StyleChip(chip, Math.Abs(brightness - _nightMode.Brightness) < 0.001f);
    }

    private void BuildBrightnessBar()
    {
        foreach (var brightness in NightMode.BrightnessChoices)
        {
            var chip = CreateChip($"{(brightness * 100).ToString("0", Culture)} %");
            chip.Clicked += (_, _) =>
            {
                _nightMode.SetBrightness(brightness);
                ApplyPalette();
            };
            _brightnessChips[brightness] = chip;
            BrightnessBar.Children.Add(chip);
        }
    }

    /// <summary>
    /// Off: turns night mode on. On: opens or closes the night panel (brightness, leave), so the brightness can be set in the
    /// dark without a system dialog.
    /// </summary>
    private void OnNightClicked(object? sender, EventArgs e)
    {
        if (!_nightMode.IsOn)
        {
            _nightMode.Toggle();
            ApplyPalette();
            return;
        }
        NightPanel.IsVisible = !NightPanel.IsVisible;
        // Both live at the bottom: the brightness panel replaces an open card.
        if (NightPanel.IsVisible)
            CloseCard();
        ApplyPalette();
    }

    private void OnLeaveNightClicked(object? sender, EventArgs e)
    {
        NightPanel.IsVisible = false;
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
        // Without a location nothing can be planned; without the ISS orbit, the Moon and the planets still can.
        if (_observers.Last is null && _alerts.Problem is { } blocking)
            return $"Avisos activados, pero ahora mismo no se pueden calcular: {blocking}.";
        var problem = _alerts.Problem is { } p ? $"\n\nAhora mismo: {p}." : "";
        if (_alerts.Pending.FirstOrDefault() is not { } next)
            return $"Avisos activados. Nada que avisar en los próximos {_alerts.PlanningDays} días: ni pasos visibles de la ISS " +
                $"ni la Luna junto a un planeta, ni planetas juntos, ni un buen día para ver Mercurio. Se vuelve a mirar cada día y cada vez que abres la app.{problem}";
        var at = TimeZoneInfo.ConvertTime(next.NotifyAt, TimeZoneInfo.Local);
        return $"Avisos activados. Próximo aviso: {at.ToString("ddd d HH:mm", SpanishCulture)}\n\n{next.Body}{problem}";
    }

    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>An alert was tapped: guide to what it announced.</summary>
    private void OnLaunchRequested() => MainThread.BeginInvokeOnMainThread(async () =>
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
        // The chip may be off screen (the ISS is the last one on most phones): show which target the HUD is guiding to.
        // On a new activity the bar is not laid out yet when the HUD appears: scroll once the chip has its size.
        var chip = _chips[target];
        if (chip.Width > 0)
        {
            await TargetScroll.ScrollToAsync(chip, ScrollToPosition.MakeVisible, false);
            return;
        }
        void OnSized(object? sender, EventArgs e)
        {
            chip.SizeChanged -= OnSized;
            _ = TargetScroll.ScrollToAsync(chip, ScrollToPosition.MakeVisible, false);
        }
        chip.SizeChanged += OnSized;
    }

    private async void SelectTarget(SkyTarget? target)
    {
        _target = target;
        _onTarget = false;
        _skyComputedAt = DateTimeOffset.MinValue;
        CloseCard();
        _cardOffer.Clear();
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
        // The HUD is held up to the sky, untouched: without this the screen dims and turns off after the system timeout
        // (30 s on the OPPO), which in night mode looked like the screen going dark.
        DeviceDisplay.Current.KeepScreenOn = true;
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
            _locationFailed = false;
        }
        else
        {
            _locationFailed = true;
        }

        await PrepareSatellitesAsync();
    }

    protected override void OnDisappearing()
    {
        DeviceDisplay.Current.KeepScreenOn = false;
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

        var identified = _target is null && pointing is { } here ? Identify(here, now) : null;
        UpdateCard(guidance, identified, now);

        _drawable.Frame = new HudFrame
        {
            TargetName = _target?.Name ?? "",
            Target = targetPosition,
            Unavailable = _target?.Unavailable,
            Pointing = pointing,
            Guidance = guidance,
            HasLocation = _observer is not null,
            LocationFailed = _locationFailed,
            Pulse = (now - _started).TotalSeconds % 1.0,
            References = _sky
                .Where(s => s.Target != _target)
                .Select(s => new ReferenceObject(s.Target.Name, s.Position, (s.Target as StarTarget)?.Star.Magnitude))
                .ToList(),
            NeedsCalibration = _pointing.Accuracy.NeedsCalibration(),
            IdentifyMode = _target is null,
            Identified = identified,
            PointingConstellation = underReticle is { } c ? SpanishNames.Constellation(c) : null,
            TargetConstellation = _targetConstellation,
            ConstellationFigure = _figure,
            ConstellationFigureName = underReticle is { } n ? SpanishNames.WithoutArticle(SpanishNames.Constellation(n)) : null,
        };
        Canvas.Invalidate();
    }

    /// <summary>
    /// Every frame: VER FICHA is offered for the chosen target all along in guide mode, filled on AQUÍ; in identify mode, for
    /// what the reticle is on, kept a little while the reticle wobbles. The card never opens by itself (decision 045): it
    /// covered the end of the guide and, once closed, could not be opened again. An open card stays open and its facts are
    /// refreshed now and then.
    /// </summary>
    private void UpdateCard(Guidance? guidance, IdentifyResult? identified, DateTimeOffset now)
    {
        if (CardPanel.IsVisible)
        {
            if (_cardTarget is { } open && now - _cardBuiltAt > CardRefreshInterval)
                ShowCard(open, now);
            return;
        }

        if (_target is { } target)
        {
            _offered = CardBuilder.HasCard(target) ? target : null;
            LightCardButton(guidance is { IsOnTarget: true });
        }
        else
        {
            var matched = identified is { IsMatch: true } m
                ? _catalog.All.FirstOrDefault(t => t.Name == m.Name && CardBuilder.HasCard(t))
                : null;
            _offered = _cardOffer.Update(matched, now);
            LightCardButton(true);
        }
        // The brightness panel lives at the bottom too.
        ShowCardButton.IsVisible = _offered is not null && !NightPanel.IsVisible;
    }

    private void LightCardButton(bool lit)
    {
        if (_cardButtonLit == lit)
            return;
        _cardButtonLit = lit;
        StyleChip(ShowCardButton, lit);
    }

    private void OnShowCardClicked(object? sender, EventArgs e)
    {
        if (_offered is { } target)
            ShowCard(target, DateTimeOffset.UtcNow);
    }

    private void OnCloseCardClicked(object? sender, EventArgs e) => CloseCard();

    private void ShowCard(SkyTarget target, DateTimeOffset now)
    {
        if (_observer is not { } observer)
            return;

        var located = _sky.FirstOrDefault(s => s.Target == target);
        var constellation = located.Target is null
            ? null
            : ConstellationAt(located.Position.AzimuthDegrees, located.Position.AltitudeDegrees, now);
        var view = _cards.Build(target, constellation, observer, now);

        CardTitle.Text = view.Title;
        CardSubtitle.Text = view.Subtitle;
        _cardDrawing.Picture = view.Picture;
        CardPictureView.IsVisible = view.Picture is not null;
        CardPictureView.HeightRequest = CardDrawing.HeightFor(view.Picture, CardContentWidth);
        CardPictureView.Invalidate();
        CardBody.Text = view.Text;
        CardBody.IsVisible = view.Text is not null;
        CardNowHeader.Text = view.FactsHeader;
        CardNowHeader.IsVisible = view.Now.Count > 0;
        CardNow.Children.Clear();
        foreach (var line in view.Now)
        {
            var label = new Label { Text = line, FontFamily = "OpenSansRegular", FontSize = 14 };
            label.SetDynamicResource(Label.TextColorProperty, "HudText");
            CardNow.Children.Add(label);
        }

        var opening = !CardPanel.IsVisible;
        // A card always opens small, not to cover the guide; the 10 s refresh keeps it as the user left it.
        if (opening || target != _cardTarget)
            _cardExpanded = false;
        _cardTarget = target;
        _cardBuiltAt = now;
        NightPanel.IsVisible = false;
        ShowCardButton.IsVisible = false;
        FitCard();
        CardPanel.IsVisible = true;
        if (opening)
            _ = CardScroll.ScrollToAsync(0, 0, false);
    }

    /// <summary>
    /// A ScrollView takes all the height it is allowed, which left a gap under the footer on short cards: give it the height
    /// of its content instead, up to <see cref="CardCollapsedHeight"/>. When that is not enough, VER MÁS shows and grows the
    /// card up to the HUD's height; only beyond that (large system fonts) does it scroll. Nobody noticed a card could scroll.
    /// </summary>
    private void FitCard()
    {
        var width = CardContentWidth;
        if (width <= 0)
            return;
        var content = CardContent.Measure(width, double.PositiveInfinity).Height;
        var footer = CardFooter.Measure(width, double.PositiveInfinity).Height;
        // The HUD's row less the card's margin, padding and border, the footer and its spacing, and a little air above.
        var expanded = Math.Max(CardCollapsedHeight, Canvas.Height - (4 + 2 * 14 + 2 + 10 + 12) - footer);

        var fits = content <= CardCollapsedHeight;
        if (fits)
            _cardExpanded = false;
        CardMoreButton.IsVisible = !fits;
        CardMoreButton.Text = _cardExpanded ? "VER MENOS" : "VER MÁS";
        CardScroll.HeightRequest = Math.Min(content, _cardExpanded ? expanded : CardCollapsedHeight);
    }

    /// <summary>Of the content above the footer: with the footer, about the 440 dp the card had before it (decision 038).</summary>
    private const double CardCollapsedHeight = 400;

    private bool _cardExpanded;

    private void OnCardMoreClicked(object? sender, EventArgs e)
    {
        _cardExpanded = !_cardExpanded;
        FitCard();
        if (!_cardExpanded)
            _ = CardScroll.ScrollToAsync(0, 0, false);
    }

    /// <summary>The page's width less the card's margins and padding, as in the XAML.</summary>
    private double CardContentWidth => Width - (2 * 12 + 2 * 16);

    private void CloseCard()
    {
        CardPanel.IsVisible = false;
        _cardTarget = null;
    }

    /// <summary>Back closes the card first, as it would a dialog.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (!CardPanel.IsVisible)
            return base.OnBackButtonPressed();
        CloseCard();
        return true;
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

    private async void OnEventsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(EventsPage));
    }

    // Diagnostics is reached from there: it is for finding problems, not for everyday use.
    private async void OnAboutClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(AboutPage));
    }
}
