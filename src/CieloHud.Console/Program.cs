using CieloHud.Console;
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
System.Console.WriteLine(SkyTableFormatter.Header(options.Observer, options.Instant));

var rows = new List<SkyRow>();

var solarSystem = new AstronomyEngineSolarSystemService();
foreach (var body in Enum.GetValues<CelestialBody>())
    rows.Add(new SkyRow(SkyTableFormatter.Name(body), solarSystem.Locate(body, options.Observer, options.Instant)));

string? tleInfo = null;
try
{
    var cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CieloHud");
    using var http = new HttpClient();
    http.DefaultRequestHeaders.UserAgent.ParseAdd("CieloHud/0.1");
    var tle = await new CelesTrakTleProvider(http, cacheDirectory).GetTleAsync(IssNoradNumber);

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
