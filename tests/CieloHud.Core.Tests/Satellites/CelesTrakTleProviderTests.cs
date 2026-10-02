using System.Net;

namespace CieloHud.Core.Tests.Satellites;

public sealed class CelesTrakTleProviderTests : IDisposable
{
    private const int Iss = 25544;
    private static readonly DateTimeOffset T0 = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(), "CieloHud.Tests", Guid.NewGuid().ToString("N"));
    private readonly FakeClock _clock = new(T0);
    private readonly FakeHttpHandler _http = new();

    private CelesTrakTleProvider CreateProvider() =>
        new(new HttpClient(_http), _cacheDir, _clock, baseUrl: "https://celestrak.test/gp.php");

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }

    [Fact]
    public async Task NoCache_DownloadsAndWritesCache()
    {
        _http.Respond(CelesTrakBody(TleTests.IssLine1));
        var provider = CreateProvider();

        var tle = await provider.GetTleAsync(Iss);

        Assert.Equal(TleTests.Iss, tle);
        Assert.Equal(1, _http.RequestCount);
        Assert.Contains("CATNR=25544", _http.LastUrl);
        Assert.Contains("FORMAT=TLE", _http.LastUrl);
        Assert.True(File.Exists(provider.GetCachePath(Iss)));
    }

    [Fact]
    public async Task FreshCache_DoesNotHitNetwork()
    {
        _http.Respond(CelesTrakBody(TleTests.IssLine1));
        var provider = CreateProvider();
        await provider.GetTleAsync(Iss);

        _clock.Advance(TimeSpan.FromHours(23));
        var tle = await provider.GetTleAsync(Iss);

        Assert.Equal(TleTests.Iss, tle);
        Assert.Equal(1, _http.RequestCount);
    }

    [Fact]
    public async Task StaleCache_DownloadsAgain()
    {
        _http.Respond(CelesTrakBody(TleTests.IssLine1));
        var provider = CreateProvider();
        await provider.GetTleAsync(Iss);

        _clock.Advance(TimeSpan.FromHours(25));
        _http.Respond(CelesTrakBody(NewerLine1));
        var tle = await provider.GetTleAsync(Iss);

        Assert.Equal(2, _http.RequestCount);
        Assert.Equal(NewerLine1, tle.Line1);
    }

    [Fact]
    public async Task StaleCache_WhenDownloadFails_ReturnsStaleTle()
    {
        _http.Respond(CelesTrakBody(TleTests.IssLine1));
        var provider = CreateProvider();
        await provider.GetTleAsync(Iss);

        _clock.Advance(TimeSpan.FromDays(3));
        _http.Fail(HttpStatusCode.ServiceUnavailable);
        var tle = await provider.GetTleAsync(Iss);

        Assert.Equal(TleTests.Iss, tle);
        Assert.Equal(2, _http.RequestCount);
    }

    [Fact]
    public async Task NoCache_WhenDownloadFails_Throws()
    {
        _http.Fail(HttpStatusCode.ServiceUnavailable);
        var provider = CreateProvider();

        var ex = await Assert.ThrowsAsync<TleUnavailableException>(() => provider.GetTleAsync(Iss));

        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task NoCache_WhenCelesTrakHasNoData_Throws()
    {
        _http.Respond("No GP data found\r\n");
        var provider = CreateProvider();

        var ex = await Assert.ThrowsAsync<TleUnavailableException>(() => provider.GetTleAsync(Iss));

        Assert.IsType<FormatException>(ex.InnerException);
    }

    [Fact]
    public async Task CorruptCache_IsIgnoredAndRedownloaded()
    {
        var provider = CreateProvider();
        Directory.CreateDirectory(_cacheDir);
        await File.WriteAllTextAsync(provider.GetCachePath(Iss), "garbage");
        _http.Respond(CelesTrakBody(TleTests.IssLine1));

        var tle = await provider.GetTleAsync(Iss);

        Assert.Equal(TleTests.Iss, tle);
        Assert.Equal(1, _http.RequestCount);
    }

    [Fact]
    public async Task CacheFile_HoldsTimestampAndThreeTleLines()
    {
        _http.Respond(CelesTrakBody(TleTests.IssLine1));
        var provider = CreateProvider();
        await provider.GetTleAsync(Iss);

        var lines = await File.ReadAllLinesAsync(provider.GetCachePath(Iss));

        Assert.Equal(4, lines.Length);
        Assert.Equal(T0, DateTimeOffset.Parse(lines[0]));
        Assert.Equal(TleTests.IssName, lines[1]);
        Assert.Equal(TleTests.IssLine1, lines[2]);
        Assert.Equal(TleTests.IssLine2, lines[3]);
    }

    // Same ISS elements with the epoch moved one day later (26275.x), checksum left as-is because Tle does not verify it.
    private const string NewerLine1 = "1 25544U 98067A   26275.82022115  .00003852  00000+0  78825-4 0  9993";

    private static string CelesTrakBody(string line1) =>
        $"{TleTests.IssName}             \r\n{line1}\r\n{TleTests.IssLine2}\r\n";

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
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
