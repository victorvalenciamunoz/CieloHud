using System.Globalization;
using CieloHud.Core.Sky;
using CieloHud.Core.SolarSystem;

namespace CieloHud.Console;

/// <summary>
/// Parsed command line: observer, instant and an optional listing (visible passes, conjunctions or Mercury) for some days,
/// or the facts of an object's card. Defaults to Madrid, now.
/// </summary>
public sealed record CommandLineOptions(
    Observer Observer, DateTimeOffset Instant, int? PassesDays, int? ConjunctionsDays = null, int? MercuryDays = null, CelestialBody? CardBody = null)
{
    public const string Usage = """
        Usage: CieloHud.Console [--lat <degrees>] [--lon <degrees>] [--alt <meters>] [--time <ISO-8601>]
                                [--passes <days> | --conjunctions <days> | --mercury <days> | --card <object>]

          --lat     Latitude, positive north.  Default 40.4168 (Madrid)
          --lon     Longitude, positive east.  Default -3.7038 (Madrid)
          --alt     Height above sea level in meters.  Default 650
          --time    Instant, e.g. 2026-10-02T21:00:00Z or 2026-10-02T23:00:00+02:00.
                    Without offset it is read as UTC.  Default: now
          --passes  Instead of the sky table, list visible ISS passes for this many days from --time (1-30).
          --conjunctions
                    Instead of the sky table, list conjunctions (Moon-planet, planet-planet) for this many days from --time (1-1100).
          --mercury Instead of the sky table, list the seasons when Mercury can be seen, and each of their days,
                    for this many days from --time (1-1100).
          --card    Instead of the sky table, the facts of the moment on an object's card at --time:
                    moon, mercury, venus, mars, jupiter or saturn.
          --help    Show this text

        Numbers use a decimal point regardless of system locale.
        """;

    private static readonly Observer Madrid = new(latitudeDegrees: 40.4168, longitudeDegrees: -3.7038, altitudeMeters: 650);

    public static CommandLineOptions Parse(string[] args)
    {
        double lat = Madrid.LatitudeDegrees, lon = Madrid.LongitudeDegrees, alt = Madrid.AltitudeMeters;
        DateTimeOffset instant = DateTimeOffset.UtcNow;
        int? passesDays = null, conjunctionsDays = null, mercuryDays = null;
        CelestialBody? cardBody = null;

        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            if (key is "--help" or "-h" or "/?")
                throw new UsageException(null);
            if (i + 1 >= args.Length)
                throw new UsageException($"Missing value for {key}.");
            var value = args[++i];

            switch (key)
            {
                case "--lat": lat = ParseDouble(key, value); break;
                case "--lon": lon = ParseDouble(key, value); break;
                case "--alt": alt = ParseDouble(key, value); break;
                case "--time": instant = ParseInstant(value); break;
                case "--passes": passesDays = ParseDays(key, value, 30); break;
                case "--conjunctions": conjunctionsDays = ParseDays(key, value, 1100); break;
                case "--mercury": mercuryDays = ParseDays(key, value, 1100); break;
                case "--card": cardBody = ParseBody(value); break;
                default: throw new UsageException($"Unknown option {key}.");
            }
        }

        try
        {
            if (new[] { passesDays, conjunctionsDays, mercuryDays }.Count(d => d is not null) + (cardBody is null ? 0 : 1) > 1)
                throw new UsageException("Use only one of --passes, --conjunctions, --mercury and --card.");
            return new CommandLineOptions(new Observer(lat, lon, alt), instant, passesDays, conjunctionsDays, mercuryDays, cardBody);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new UsageException(ex.Message);
        }
    }

    private static double ParseDouble(string key, string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new UsageException($"{key} expects a number with a decimal point, got '{value}'.");

    private static int ParseDays(string key, string value, int max) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) && days >= 1 && days <= max
            ? days
            : throw new UsageException($"{key} expects a whole number of days from 1 to {max}, got '{value}'.");

    private static CelestialBody ParseBody(string value) =>
        Enum.TryParse<CelestialBody>(value, ignoreCase: true, out var body) && Enum.IsDefined(body) && !int.TryParse(value, out _)
            ? body
            : throw new UsageException($"--card expects moon, mercury, venus, mars, jupiter or saturn, got '{value}'.");

    private static DateTimeOffset ParseInstant(string value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result)
            ? result
            : throw new UsageException($"--time expects an ISO-8601 instant, got '{value}'.");
}

/// <summary>Bad command line. A null message means the user asked for help.</summary>
public sealed class UsageException(string? message) : Exception(message ?? string.Empty);
