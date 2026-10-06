namespace CieloHud.App;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(DiagnosticsPage), typeof(DiagnosticsPage));
		Routing.RegisterRoute(nameof(EventsPage), typeof(EventsPage));
		Routing.RegisterRoute(nameof(AboutPage), typeof(AboutPage));
	}
}
