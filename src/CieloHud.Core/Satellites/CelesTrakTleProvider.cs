using System.Globalization;

namespace CieloHud.Core.Satellites;

/// <summary>
/// Downloads TLEs from CelesTrak's GP API and caches each one in a text file.
/// A cached TLE younger than <see cref="MaxCacheAge"/> is served without touching the network.
/// If the download fails and a stale cache exists, the stale TLE is returned so the app keeps working offline.
/// </summary>
public sealed class CelesTrakTleProvider : ITleProvider
{
    public const string DefaultBaseUrl = "https://celestrak.org/NORAD/elements/gp.php";
    public static readonly TimeSpan DefaultMaxCacheAge = TimeSpan.FromHours(24);

    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly string _baseUrl;

    public TimeSpan MaxCacheAge { get; }

    /// <param name="httpClient">Client used for downloads. The caller owns its lifetime.</param>
    /// <param name="cacheDirectory">Writable directory for cache files; created on first write.</param>
    /// <param name="maxCacheAge">How long a downloaded TLE is reused before re-downloading. Defaults to 24 h.</param>
    /// <param name="baseUrl">CelesTrak GP endpoint; overridable for tests.</param>
    public CelesTrakTleProvider(
        HttpClient httpClient,
        string cacheDirectory,
        TimeSpan? maxCacheAge = null,
        string baseUrl = DefaultBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        _httpClient = httpClient;
        _cacheDirectory = cacheDirectory;
        MaxCacheAge = maxCacheAge ?? DefaultMaxCacheAge;
        _baseUrl = baseUrl;
    }

    public async Task<Tle> GetTleAsync(int noradNumber, CancellationToken cancellationToken = default)
    {
        var cached = await ReadCacheAsync(noradNumber, cancellationToken).ConfigureAwait(false);
        if (cached is { } entry && DateTimeOffset.UtcNow - entry.DownloadedAt < MaxCacheAge)
            return entry.Tle;

        try
        {
            var tle = await DownloadAsync(noradNumber, cancellationToken).ConfigureAwait(false);
            await WriteCacheAsync(noradNumber, tle, cancellationToken).ConfigureAwait(false);
            return tle;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException)
        {
            if (cached is { } stale)
                return stale.Tle;
            throw new TleUnavailableException($"Could not download TLE for NORAD {noradNumber} and no cached copy exists.", ex);
        }
    }

    public string GetCachePath(int noradNumber) => Path.Combine(_cacheDirectory, $"tle-{noradNumber}.txt");

    private async Task<Tle> DownloadAsync(int noradNumber, CancellationToken cancellationToken)
    {
        var url = $"{_baseUrl}?CATNR={noradNumber}&FORMAT=TLE";
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        // CelesTrak answers 200 with "No GP data found" for unknown catalog numbers; Tle.Parse turns that into FormatException.
        var tle = Tle.Parse(body);
        if (tle.NoradNumber != noradNumber)
            throw new FormatException($"Requested NORAD {noradNumber} but CelesTrak returned {tle.NoradNumber}.");
        return tle;
    }

    // Cache file: ISO-8601 download time, then the three TLE lines.
    private async Task<CacheEntry?> ReadCacheAsync(int noradNumber, CancellationToken cancellationToken)
    {
        var path = GetCachePath(noradNumber);
        if (!File.Exists(path))
            return null;

        try
        {
            var lines = await File.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false);
            if (lines.Length < 4)
                return null;
            var downloadedAt = DateTimeOffset.Parse(lines[0], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            var tle = new Tle(lines[1], lines[2], lines[3]);
            return new CacheEntry(downloadedAt, tle);
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            // A corrupt or unreadable cache is the same as no cache.
            return null;
        }
    }

    private async Task WriteCacheAsync(int noradNumber, Tle tle, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_cacheDirectory);
        var lines = new[]
        {
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            tle.Name,
            tle.Line1,
            tle.Line2,
        };
        await File.WriteAllLinesAsync(GetCachePath(noradNumber), lines, cancellationToken).ConfigureAwait(false);
    }

    private readonly record struct CacheEntry(DateTimeOffset DownloadedAt, Tle Tle);
}

/// <summary>Thrown when no TLE can be obtained from the network nor from the cache.</summary>
public sealed class TleUnavailableException : Exception
{
    public TleUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}
