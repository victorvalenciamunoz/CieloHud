using CieloHud.App.Hud;
using Microsoft.Extensions.DependencyInjection;

namespace CieloHud.App;

public partial class App : Application
{
	private readonly NightMode _nightMode;

	public App(NightMode nightMode)
	{
		InitializeComponent();
		_nightMode = nightMode;
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
		window.Activated += (_, _) => _nightMode.ApplyToWindow();
		return window;
	}
}