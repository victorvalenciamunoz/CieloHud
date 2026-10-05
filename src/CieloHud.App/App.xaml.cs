using CieloHud.App.Alerts;
using CieloHud.App.Hud;
using Microsoft.Extensions.DependencyInjection;

namespace CieloHud.App;

public partial class App : Application
{
	private readonly NightMode _nightMode;
	private readonly PassAlertService _alerts;

	public App(NightMode nightMode, PassAlertService alerts)
	{
		InitializeComponent();
		_nightMode = nightMode;
		_alerts = alerts;
		// Before any page is built, so their DynamicResource colors are there from the first frame.
		nightMode.Apply();
		// A HUD for the night sky: always dark, whatever the system theme.
		UserAppTheme = AppTheme.Dark;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());
		// Every time the app comes to the front: the activity window exists only from now on, and Android may have
		// brought the system bars back while we were away.
		window.Activated += (_, _) =>
		{
			_nightMode.ApplyToWindow();
			// Opening the app re-arms the alerts: a fresher TLE, maybe a new place, or the user back from the exact-alarm settings.
			if (_alerts.IsOn)
				_ = Task.Run(_alerts.RescheduleAsync);
		};
		return window;
	}
}
