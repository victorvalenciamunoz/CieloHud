using CieloHud.App.Hud;
using Microsoft.Extensions.DependencyInjection;

namespace CieloHud.App;

public partial class App : Application
{
	public App(NightMode nightMode)
	{
		InitializeComponent();
		// Before any page is built, so their DynamicResource colors are there from the first frame.
		nightMode.Apply();
		// A HUD for the night sky: always dark, whatever the system theme.
		UserAppTheme = AppTheme.Dark;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}