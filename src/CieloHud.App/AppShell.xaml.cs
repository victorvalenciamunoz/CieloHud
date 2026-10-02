namespace CieloHud.App;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(DiagnosticsPage), typeof(DiagnosticsPage));
	}
}
