using CieloHud.App.Hud;
using CieloHud.App.Services;
using CieloHud.Core.Satellites;
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
		builder.Services.AddSingleton<ISatelliteService, Sgp4SatelliteService>();
		builder.Services.AddSingleton<CieloHud.Core.Stars.IStarService, CieloHud.Core.Stars.AstronomyEngineStarService>();
		builder.Services.AddSingleton<CieloHud.Core.Constellations.IConstellationLocator, CieloHud.Core.Constellations.AstronomyEngineConstellationLocator>();
		builder.Services.AddSingleton<CieloHud.Core.Constellations.IConstellationFigureLocator, CieloHud.Core.Constellations.AstronomyEngineConstellationFigureLocator>();
		builder.Services.AddSingleton(_ =>
		{
			var http = new HttpClient();
			http.DefaultRequestHeaders.UserAgent.ParseAdd("CieloHud/0.1");
			return http;
		});
		builder.Services.AddSingleton<ITleProvider>(sp =>
			new CelesTrakTleProvider(sp.GetRequiredService<HttpClient>(), Path.Combine(FileSystem.AppDataDirectory, "tle")));

		// Device
#if ANDROID
		builder.Services.AddSingleton<IPointingSource, Platforms.Android.RotationVectorPointingSource>();
#else
		builder.Services.AddSingleton<IPointingSource, OrientationSensorPointingSource>();
#endif
		builder.Services.AddSingleton<ILocationSource, GeolocationSource>();

		// HUD
		builder.Services.AddSingleton<TargetCatalog>();
		builder.Services.AddTransient<HudPage>();
		builder.Services.AddTransient<DiagnosticsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
