using CieloHud.App.Services;
using CieloHud.Core.SolarSystem;
using Microsoft.Extensions.Logging;

namespace CieloHud.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddFont("ChakraPetch-Regular.ttf", "ChakraPetch");
				fonts.AddFont("ChakraPetch-Bold.ttf", "ChakraPetchBold");
			});

		// Core (no UI, no state)
		builder.Services.AddSingleton<ISolarSystemService, AstronomyEngineSolarSystemService>();
		builder.Services.AddSingleton<ISunService, AstronomyEngineSunService>();

		// Device
		builder.Services.AddSingleton<IPointingSource, OrientationSensorPointingSource>();
		builder.Services.AddSingleton<ILocationSource, GeolocationSource>();

		// Pages
		builder.Services.AddTransient<DiagnosticsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
