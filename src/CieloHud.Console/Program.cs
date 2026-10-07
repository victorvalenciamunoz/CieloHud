using CieloHud.Console;
using CieloHud.Core.Alerts;
using CieloHud.Core.Apparitions;
using CieloHud.Core.Cards;
using CieloHud.Core.Conjunctions;
using CieloHud.Core.Passes;
using CieloHud.Core.Satellites;
using CieloHud.Core.SolarSystem;

const int IssNoradNumber = 25544;

CommandLineOptions options;
try
{
    options = CommandLineOptions.Parse(args);
}
catch (UsageException ex) when (ex.Message.Length == 0)
{
    System.Console.WriteLine(CommandLineOptions.Usage);
    return 0;
}
catch (UsageException ex)
{
    System.Console.Error.WriteLine($"Error: {ex.Message}\n");
    System.Console.Error.WriteLine(CommandLineOptions.Usage);
    return 1;
}

System.Console.OutputEncoding = System.Text.Encoding.UTF8;

var cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CieloHud");
using var http = new HttpClient();
http.DefaultRequestHeaders.UserAgent.ParseAdd("CieloHud/0.1");
var tleProvider = new CelesTrakTleProvider(http, cacheDirectory);

if (options.ConjunctionsDays is { } conjunctionDays)
    return PrintConjunctions(conjunctionDays);
if (options.MercuryDays is { } mercuryDays)
    return PrintMercury(mercuryDays);
if (options.CardBody is { } cardBody)
    return PrintCard(cardBody);

return options.PassesDays is { } days
    ? await PrintPassesAsync(days)
    : await PrintSkyTableAsync();

async Task<int> PrintSkyTableAsync()
{
    System.Console.WriteLine(SkyTableFormatter.Header(options.Observer, options.Instant));

    var rows = new List<SkyRow>();
    var solarSystem = new AstronomyEngineSolarSystemService();
    foreach (var body in Enum.GetValues<CelestialBody>())
        rows.Add(new SkyRow(SkyTableFormatter.Name(body), solarSystem.Locate(body, options.Observer, options.Instant)));

    string? tleInfo = null;
    try
    {
        var tle = await tleProvider.GetTleAsync(IssNoradNumber);
        rows.Add(new SkyRow("ISS", new Sgp4SatelliteService().Locate(tle, options.Observer, options.Instant)));
        tleInfo = SkyTableFormatter.TleInfo(tle, options.Instant);
    }
    catch (TleUnavailableException)
    {
        rows.Add(new SkyRow("ISS", null, "sin TLE (sin red)"));
    }

    System.Console.WriteLine(SkyTableFormatter.Table(rows));
    if (tleInfo is not null)
        System.Console.WriteLine(tleInfo);
    return 0;
}

async Task<int> PrintPassesAsync(int days)
{
    Tle tle;
    try
    {
        tle = await tleProvider.GetTleAsync(IssNoradNumber);
    }
    catch (TleUnavailableException ex)
    {
        System.Console.Error.WriteLine($"Error: no se pudo obtener el TLE de la ISS ({ex.InnerException?.Message}).");
        return 2;
    }

    var from = options.Instant;
    var to = from.AddDays(days);
    var criteria = VisibilityCriteria.Default;
    var finder = new VisiblePassFinder(
        new Sgp4SatellitePassPredictor(),
        new Sgp4SatelliteService(),
        new AstronomyEngineSunService(),
        new Sgp4SatelliteIlluminationService(),
        criteria);

    var passes = finder.Find(tle, options.Observer, from, to);

    System.Console.WriteLine(PassTableFormatter.Header(options.Observer, from, to, criteria));
    System.Console.WriteLine(PassTableFormatter.Table(passes));
    System.Console.WriteLine(SkyTableFormatter.TleInfo(tle, from));
    return 0;
}

int PrintConjunctions(int days)
{
    var from = options.Instant;
    var to = from.AddDays(days);
    var finder = new ConjunctionFinder(new AstronomyEngineSolarSystemService(), new AstronomyEngineSunService());

    var conjunctions = finder.FindWithMoon(options.Observer, from, to, TimeZoneInfo.Local);

    System.Console.WriteLine(ConjunctionTableFormatter.Header(options.Observer, from, to, finder.Criteria));
    System.Console.WriteLine(ConjunctionTableFormatter.Table(conjunctions));
    var planetPairs = finder.FindPlanetPairs(options.Observer, from, to, TimeZoneInfo.Local);
    System.Console.WriteLine(ConjunctionTableFormatter.PlanetPairs(planetPairs));

    var alerts = new ConjunctionAlertPlanner().Plan(conjunctions.Concat(planetPairs), from, TimeZoneInfo.Local);
    System.Console.WriteLine(ConjunctionTableFormatter.Alerts(alerts, TimeZoneInfo.Local));
    return 0;
}

int PrintMercury(int days)
{
    var from = options.Instant;
    var to = from.AddDays(days);
    var finder = new MercuryApparitionFinder(
        new AstronomyEngineSolarSystemService(), new AstronomyEngineSunService(), new AstronomyEngineMagnitudeService());

    System.Console.WriteLine(MercuryTableFormatter.Header(options.Observer, from, to, finder.Criteria));
    var apparitions = finder.Find(options.Observer, from, to, TimeZoneInfo.Local);
    System.Console.WriteLine(MercuryTableFormatter.Apparitions(apparitions));
    System.Console.WriteLine(MercuryTableFormatter.Windows(finder.FindWindows(options.Observer, from, to, TimeZoneInfo.Local)));
    var alerts = new MercuryAlertPlanner().Plan(apparitions, from, TimeZoneInfo.Local);
    System.Console.WriteLine(MercuryTableFormatter.Alerts(alerts, TimeZoneInfo.Local));
    return 0;
}

int PrintCard(CelestialBody body)
{
    var facts = new AstronomyEngineSolarSystemFactsService();
    System.Console.WriteLine(SkyTableFormatter.Header(options.Observer, options.Instant));
    System.Console.WriteLine(body == CelestialBody.Moon
        ? CardFormatter.Moon(facts.Moon(options.Observer, options.Instant), CardTexts.Find(CardKey.Body(body)), options.Instant, TimeZoneInfo.Local)
        : CardFormatter.Planet(SkyTableFormatter.Name(body), facts.Planet(body, options.Observer, options.Instant), CardTexts.Find(CardKey.Body(body))));
    if (body == CelestialBody.Jupiter)
        System.Console.WriteLine(CardFormatter.JupiterMoons(facts.JupiterMoons(options.Observer, options.Instant)));
    return 0;
}
