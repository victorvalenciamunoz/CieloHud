using Microsoft.Extensions.DependencyInjection;

namespace CieloHud.App;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		// A HUD for the night sky: always dark, whatever the system theme.
		UserAppTheme = AppTheme.Dark;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}