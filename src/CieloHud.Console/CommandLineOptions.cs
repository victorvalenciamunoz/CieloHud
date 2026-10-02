using System.Globalization;
using CieloHud.Core.Sky;

namespace CieloHud.Console;

/// <summary>
/// Parsed command line: observer, instant and optional pass-prediction window. Defaults to Madrid, now.
/// </summary>
public sealed record CommandLineOptions(Observer Observer, DateTimeOffset Instant, int? PassesDays)
{
    public const string Usage = """
        Usage: CieloHud.Console [--lat <degrees>] [--lon <degrees>] [--alt <meters>] [--time <ISO-8601>] [--passes <days>]

          --lat     Latitude, positive north.  Default 40.4168 (Madrid)
          --lon     Longitude, positive east.  Default -3.7038 (Madrid)
          --alt     Height above sea level in meters.  Default 650
          --time    Instant, e.g. 2026-10-02T21:00:00Z or 2026-10-02T23:00:00+02:00.
                    Without offset it is read as UTC.  Default: now
          --passes  Instead of the sky table, list visible ISS passes for this many days from --time (1-30).
          --help    Show this text

        Numbers use a decimal point regardless of system locale.
        """;

    private static readonly Observer Madrid = new(latitudeDegrees: 40.4168, longitudeDegrees: -3.7038, altitudeMeters: 650);

    public static CommandLineOptions Parse(string[] args)
    {
        double lat = Madrid.LatitudeDegrees, lon = Madrid.LongitudeDegrees, alt = Madrid.AltitudeMeters;
        DateTimeOffset instant = DateTimeOffset.UtcNow;
        int? passesDays = null;

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
                case "--passes": passesDays = ParseDays(value); break;
                default: throw new UsageException($"Unknown option {key}.");
            }
        }

        try
        {
            return new CommandLineOptions(new Observer(lat, lon, alt), instant, passesDays);
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

    private static int ParseDays(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) && days is >= 1 and <= 30
            ? days
            : throw new UsageException($"--passes expects a whole number of days from 1 to 30, got '{value}'.");

    private static DateTimeOffset ParseInstant(string value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result)
            ? result
            : throw new UsageException($"--time expects an ISO-8601 instant, got '{value}'.");
}

/// <summary>Bad command line. A null message means the user asked for help.</summary>
public sealed class UsageException(string? message) : Exception(message ?? string.Empty);
