using CieloHud.Console;
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
