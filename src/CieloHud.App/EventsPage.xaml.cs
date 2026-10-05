using CieloHud.App.Alerts;
using CieloHud.Core.Events;

namespace CieloHud.App;

/// <summary>
/// What there is to see in the coming days (decision 030): ISS passes and conjunctions, by day, with when each will be announced.
/// Computed when the page opens, from the same finders and rules as the alerts. Tapping an event guides to it in the HUD.
/// </summary>
public partial class EventsPage : ContentPage
{
    private readonly AlertService _alerts;

    public EventsPage(AlertService alerts)
    {
        InitializeComponent();
        _alerts = alerts;
        ScopeLabel.Text = $"Lo que se ve a simple vista: la Luna y los planetas en los próximos {AlertService.EventDays} días, " +
            $"la ISS en los próximos {_alerts.PlanningDays}. Toca uno para que el HUD te lleve.";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StatusLabel.Text = "Calculando…";
        EventList.Children.Clear();

        var upcoming = await Task.Run(_alerts.UpcomingAsync);
        var now = DateTimeOffset.UtcNow;
        string? day = null;
        foreach (var e in upcoming.Events)
        {
            var label = UpcomingEvents.DayLabel(e.At, now, TimeZoneInfo.Local);
            if (label != day)
            {
                day = label;
                EventList.Children.Add(new Label { Text = label.ToUpperInvariant(), Style = (Style)Resources["Section"] });
            }
            EventList.Children.Add(EventCard(e, now));
        }

        var nothing = upcoming.Events.Count == 0 ? "Nada a la vista en estos días." : null;
        var alertsOff = upcoming.Events.Count > 0 && !_alerts.IsOn ? "Los avisos están desactivados (botón AVISOS del HUD)." : null;
        var problem = upcoming.Problem is { } p ? $"Ahora mismo: {p}." : null;
        StatusLabel.Text = string.Join("\n", new[] { nothing, alertsOff, problem }.Where(s => s is not null));
    }

    private View EventCard(SkyEvent e, DateTimeOffset now)
    {
        var grid = new Grid { ColumnDefinitions = [new ColumnDefinition(72), new ColumnDefinition(GridLength.Star)], ColumnSpacing = 12 };
        grid.Add(new Label
        {
            Text = e.Time,
            FontFamily = "ChakraPetchBold",
            FontSize = 18,
            VerticalOptions = LayoutOptions.Start,
        }.WithColor(Label.TextColorProperty, "HudMarker"));

        var text = new VerticalStackLayout { Spacing = 2 };
        text.Add(new Label { Text = e.Title, FontFamily = "ChakraPetchBold", FontSize = 16 }.WithColor(Label.TextColorProperty, "HudText"));
        text.Add(new Label { Text = e.Details, FontFamily = "OpenSansRegular", FontSize = 13 }.WithColor(Label.TextColorProperty, "HudTextSoft"));
        // Only when the alerts are on: otherwise there will be no notification.
        if (_alerts.IsOn && e.AlertAt is { } alertAt)
            text.Add(new Label
            {
                Text = UpcomingEvents.AlertLabel(alertAt, now, TimeZoneInfo.Local),
                FontFamily = "OpenSansRegular",
                FontSize = 12,
            }.WithColor(Label.TextColorProperty, "HudTextDim"));
        grid.Add(text, 1);

        var card = new Border
        {
            Content = grid,
            Padding = new Thickness(12, 10),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            BackgroundColor = Colors.Transparent,
        }.WithColor(Border.StrokeProperty, "HudTextDim");

        var tap = new TapGestureRecognizer();
        // The HUD takes the request and comes back to the front, like when an alert is tapped.
        tap.Tapped += (_, _) => LaunchRequests.Request(e.Target);
        card.GestureRecognizers.Add(tap);
        return card;
    }
}

internal static class DynamicColors
{
    /// <summary>Binds a color property to a palette resource, so the night mode recolors it like the XAML pages.</summary>
    public static T WithColor<T>(this T element, BindableProperty property, string resource) where T : Element
    {
        element.SetDynamicResource(property, resource);
        return element;
    }
}
