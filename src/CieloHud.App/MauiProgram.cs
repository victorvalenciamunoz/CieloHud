using CieloHud.App.Alerts;
using CieloHud.App.Hud;
using CieloHud.Core.Apparitions;
using CieloHud.Core.Conjunctions;
using CieloHud.Core.Passes;
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
		builder.Services.AddSingleton<IMagnitudeService, AstronomyEngineMagnitudeService>();
		builder.Services.AddSingleton<CieloHud.Core.Cards.ISolarSystemFactsService, CieloHud.Core.Cards.AstronomyEngineSolarSystemFactsService>();
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
		builder.Services.AddSingleton<ISatellitePassPredictor, Sgp4SatellitePassPredictor>();
		builder.Services.AddSingleton<ISatelliteIlluminationService, Sgp4SatelliteIlluminationService>();
		builder.Services.AddSingleton<CieloHud.Core.Cards.ISatelliteFactsService>(sp => new CieloHud.Core.Cards.Sgp4SatelliteFactsService(
			sp.GetRequiredService<ISatelliteService>(),
			sp.GetRequiredService<ISatelliteIlluminationService>(),
			sp.GetRequiredService<ISunService>()));
		builder.Services.AddSingleton<IVisiblePassFinder>(sp => new VisiblePassFinder(
			sp.GetRequiredService<ISatellitePassPredictor>(),
			sp.GetRequiredService<ISatelliteService>(),
			sp.GetRequiredService<ISunService>(),
			sp.GetRequiredService<ISatelliteIlluminationService>()));
		builder.Services.AddSingleton<IConjunctionFinder>(sp => new ConjunctionFinder(
			sp.GetRequiredService<ISolarSystemService>(),
			sp.GetRequiredService<ISunService>()));
		builder.Services.AddSingleton<IMercuryApparitionFinder>(sp => new MercuryApparitionFinder(
			sp.GetRequiredService<ISolarSystemService>(),
			sp.GetRequiredService<ISunService>(),
			sp.GetRequiredService<IMagnitudeService>()));

		// Device
#if ANDROID
		builder.Services.AddSingleton<IPointingSource, Platforms.Android.RotationVectorPointingSource>();
#else
		builder.Services.AddSingleton<IPointingSource, OrientationSensorPointingSource>();
#endif
		builder.Services.AddSingleton<ILocationSource, GeolocationSource>();
		builder.Services.AddSingleton<ObserverStore>();

		// Alerts (phase 4)
#if ANDROID
		builder.Services.AddSingleton<IAlertPlatform, Platforms.Android.Alerts.AndroidAlertPlatform>();
#endif
		builder.Services.AddSingleton<AlertService>();

		// HUD
		builder.Services.AddSingleton<TargetCatalog>();
		builder.Services.AddSingleton<CardBuilder>();
		builder.Services.AddSingleton(Preferences.Default);
		builder.Services.AddSingleton<NightMode>();
		builder.Services.AddTransient<HudPage>();
		builder.Services.AddTransient<DiagnosticsPage>();
		builder.Services.AddTransient<EventsPage>();
		builder.Services.AddTransient<AboutPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
