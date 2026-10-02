using System.Globalization;
using System.Net;

namespace CieloHud.Core.Tests.Satellites;

public sealed class CelesTrakTleProviderTests : IDisposable
{
    private const int Iss = 25544;

    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(), "CieloHud.Tests", Guid.NewGuid().ToString("N"));
    private readonly FakeHttpHandler _http = new();
    private readonly CelesTrakTleProvider _provider;

    public CelesTrakTleProviderTests()
    {
        _provider = new CelesTrakTleProvider(new HttpClient(_http), _cacheDir, baseUrl: "https://celestrak.test/gp.php");
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }

    [Fact]
    public async Task NoCache_DownloadsAndWritesCache()
    {
        _http.Respond(CelesTrakBody(TleTests.IssLine1));

        var tle = await _provider.GetTleAsync(Iss);

        Assert.Equal(TleTests.Iss, tle);
        Assert.Equal(1, _http.RequestCount);
        Assert.Contains("CATNR=25544", _http.LastUrl);
        Assert.Contains("FORMAT=TLE", _http.LastUrl);
        Assert.True(File.Exists(_provider.GetCachePath(Iss)));
    }

    [Fact]
    public async Task FreshCache_DoesNotHitNetwork()
    {
        _http.Respond(CelesTrakBody(TleTests.IssLine1));
        await _provider.GetTleAsync(Iss);

        var tle = await _provider.GetTleAsync(Iss);

        Assert.Equal(TleTests.Iss, tle);
        Assert.Equal(1, _http.RequestCount);
    }

    [Fact]
    public async Task StaleCache_DownloadsAgain()
    {
        await SeedCacheAsync(downloadedAt: DateTimeOffset.UtcNow - TimeSpan.FromHours(25), TleTests.IssLine1);
        _http.Respond(CelesTrakBody(NewerLine1));

        var tle = await _provider.GetTleAsync(Iss);

        Assert.Equal(1, _http.RequestCount);
        Assert.Equal(NewerLine1, tle.Line1);
    }

    [Fact]
    public async Task StaleCache_WhenDownloadFails_ReturnsStaleTle()
    {
        await SeedCacheAsync(downloadedAt: DateTimeOffset.UtcNow - TimeSpan.FromDays(3), TleTests.IssLine1);
        _http.Fail(HttpStatusCode.ServiceUnavailable);

        var tle = await _provider.GetTleAsync(Iss);

        Assert.Equal(TleTests.Iss, tle);
        Assert.Equal(1, _http.RequestCount);
    }

    [Fact]
    public async Task NoCache_WhenDownloadFails_Throws()
    {
        _http.Fail(HttpStatusCode.ServiceUnavailable);

        var ex = await Assert.ThrowsAsync<TleUnavailableException>(() => _provider.GetTleAsync(Iss));

        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task NoCache_WhenCelesTrakHasNoData_Throws()
    {
        _http.Respond("No GP data found\r\n");

        var ex = await Assert.ThrowsAsync<TleUnavailableException>(() => _provider.GetTleAsync(Iss));

        Assert.IsType<FormatException>(ex.InnerException);
    }

    [Fact]
    public async Task CorruptCache_IsIgnoredAndRedownloaded()
    {
        Directory.CreateDirectory(_cacheDir);
        await File.WriteAllTextAsync(_provider.GetCachePath(Iss), "garbage");
        _http.Respond(CelesTrakBody(TleTests.IssLine1));

        var tle = await _provider.GetTleAsync(Iss);

        Assert.Equal(TleTests.Iss, tle);
        Assert.Equal(1, _http.RequestCount);
    }

    [Fact]
    public async Task CacheFile_HoldsDownloadTimeAndThreeTleLines()
    {
        _http.Respond(CelesTrakBody(TleTests.IssLine1));
        var before = DateTimeOffset.UtcNow;

        await _provider.GetTleAsync(Iss);

        var lines = await File.ReadAllLinesAsync(_provider.GetCachePath(Iss));
        Assert.Equal(4, lines.Length);
        var downloadedAt = DateTimeOffset.Parse(lines[0], CultureInfo.InvariantCulture);
        Assert.InRange(downloadedAt, before, DateTimeOffset.UtcNow);
        Assert.Equal(TleTests.IssName, lines[1]);
        Assert.Equal(TleTests.IssLine1, lines[2]);
        Assert.Equal(TleTests.IssLine2, lines[3]);
    }

    // Same ISS elements with the epoch moved one day later (26275.x), checksum left as-is because Tle does not verify it.
    private const string NewerLine1 = "1 25544U 98067A   26275.82022115  .00003852  00000+0  78825-4 0  9993";

    private static string CelesTrakBody(string line1) =>
        $"{TleTests.IssName}             \r\n{line1}\r\n{TleTests.IssLine2}\r\n";

    /// <summary>Writes a cache file as the provider would have, but with the given download time.</summary>
    private async Task SeedCacheAsync(DateTimeOffset downloadedAt, string line1)
    {
        Directory.CreateDirectory(_cacheDir);
        var lines = new[] { downloadedAt.ToString("O", CultureInfo.InvariantCulture), TleTests.IssName, line1, TleTests.IssLine2 };
        await File.WriteAllLinesAsync(_provider.GetCachePath(Iss), lines);
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private string? _body;
        private HttpStatusCode _status = HttpStatusCode.OK;

        public int RequestCount { get; private set; }
        public string? LastUrl { get; private set; }

        public void Respond(string body) { _body = body; _status = HttpStatusCode.OK; }
        public void Fail(HttpStatusCode status) { _body = null; _status = status; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastUrl = request.RequestUri?.ToString();
            var response = new HttpResponseMessage(_status) { Content = new StringContent(_body ?? string.Empty) };
            return Task.FromResult(response);
        }
    }
}
