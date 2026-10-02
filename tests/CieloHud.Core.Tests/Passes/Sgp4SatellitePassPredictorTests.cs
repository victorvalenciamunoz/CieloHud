using CieloHud.Core.Tests.Satellites;

namespace CieloHud.Core.Tests.Passes;

/// <summary>
/// Reference events from JPL Horizons (COMMAND='-125544', Madrid, APPARENT='AIRLESS', R_T_S_ONLY='GEO', STEP_SIZE='1 m'),
/// queried on 2026-10-02 for 2026-10-01 20:00 to 2026-10-02 20:00 UTC. Horizons reports the first whole minute at or after
/// each rise/set, so times are good to ±60 s. Maximum altitudes come from the 1-minute table with ELEV_CUT='10', so the
/// true peak may be a little higher than the sampled value.
/// </summary>
public class Sgp4SatellitePassPredictorTests
{
    private static readonly Observer Madrid = new(latitudeDegrees: 40.4168, longitudeDegrees: -3.7038, altitudeMeters: 650);
    private static readonly DateTimeOffset From = DateTimeOffset.Parse("2026-10-01T20:00:00Z");
    private static readonly DateTimeOffset To = DateTimeOffset.Parse("2026-10-02T20:00:00Z");
    private static readonly TimeSpan TimeTolerance = TimeSpan.FromSeconds(60);

    private readonly Sgp4SatellitePassPredictor _predictor = new();

    [Fact]
    public void Predict_FindsEveryHorizonsPass()
    {
        var passes = _predictor.Predict(TleTests.Iss, Madrid, From, To);

        var expectedRises = new[] { "09:45", "11:21", "12:59", "14:37", "16:14", "17:51", "19:30" }
            .Select(hhmm => DateTimeOffset.Parse($"2026-10-02T{hhmm}:00Z"))
            .ToArray();
        Assert.Equal(expectedRises.Length, passes.Count);
        for (var i = 0; i < expectedRises.Length; i++)
            AssertClose(expectedRises[i], passes[i].Start.Instant, $"rise of pass {i}");
    }

    [Theory]
    [InlineData(0, "09:55")]
    [InlineData(2, "13:09")]
    [InlineData(3, "14:47")]
    [InlineData(4, "16:25")]
    public void Predict_SetTimesMatchHorizons(int passIndex, string expectedSetHhmm)
    {
        var passes = _predictor.Predict(TleTests.Iss, Madrid, From, To);

        AssertClose(DateTimeOffset.Parse($"2026-10-02T{expectedSetHhmm}:00Z"), passes[passIndex].End.Instant, "set");
    }

    [Theory]
    [InlineData(1, "11:26", 47.35)]
    [InlineData(5, "17:56", 31.63)]
    [InlineData(0, "09:50", 18.53)]
    public void Predict_MaxMatchesHorizonsSample(int passIndex, string sampledHhmm, double sampledAltitude)
    {
        var pass = _predictor.Predict(TleTests.Iss, Madrid, From, To)[passIndex];

        AssertClose(DateTimeOffset.Parse($"2026-10-02T{sampledHhmm}:00Z"), pass.Max.Instant, "max time");
        // Sampled once a minute, so the real peak is at least as high; the ISS gains at most a few degrees in 30 s near the top.
        // Azimuth is not compared here: on a low pass it changes ~25°/min, so a 1-minute sample says little about the peak.
        Assert.InRange(pass.MaxAltitudeDegrees, sampledAltitude - 0.5, sampledAltitude + 5);
    }

    [Fact]
    public void Predict_PassesStartAndEndAtTheHorizon()
    {
        var passes = _predictor.Predict(TleTests.Iss, Madrid, From, To);

        foreach (var pass in passes)
        {
            Assert.InRange(pass.Start.Position.AltitudeDegrees, -0.2, 0.2);
            Assert.InRange(pass.End.Position.AltitudeDegrees, -0.2, 0.2);
            Assert.True(pass.MaxAltitudeDegrees > pass.Start.Position.AltitudeDegrees);
            Assert.True(pass.Start.Instant < pass.Max.Instant && pass.Max.Instant < pass.End.Instant);
            Assert.InRange(pass.Duration, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(12));
        }
    }

    [Fact]
    public void Predict_PassStartedBeforeTo_IsReportedInFull()
    {
        // The 09:44-09:54 pass straddles this 'to'.
        var to = DateTimeOffset.Parse("2026-10-02T09:50:00Z");

        var passes = _predictor.Predict(TleTests.Iss, Madrid, From, to);

        var last = passes[^1];
        Assert.True(last.Start.Instant < to);
        Assert.True(last.End.Instant > to, "a pass in progress at 'to' must keep its real end");
        Assert.InRange(last.End.Position.AltitudeDegrees, -0.2, 0.2);
    }

    [Fact]
    public void Predict_PassInProgressAtFrom_StartsAtFrom()
    {
        // Same pass, entered in the middle.
        var from = DateTimeOffset.Parse("2026-10-02T09:50:00Z");

        var passes = _predictor.Predict(TleTests.Iss, Madrid, from, To);

        var first = passes[0];
        AssertClose(from, first.Start.Instant, "clipped start");
        Assert.True(first.Start.Position.AltitudeDegrees > 10, "entered mid-pass, so already well above the horizon");
    }

    [Fact]
    public void Predict_ReturnsInstantsInUtc()
    {
        var pass = _predictor.Predict(TleTests.Iss, Madrid, From, To)[0];

        Assert.Equal(TimeSpan.Zero, pass.Start.Instant.Offset);
    }

    [Fact]
    public void Predict_RejectsEmptyRange()
    {
        Assert.Throws<ArgumentException>(() => _predictor.Predict(TleTests.Iss, Madrid, From, From));
    }

    private static void AssertClose(DateTimeOffset expected, DateTimeOffset actual, string what)
    {
        var delta = actual - expected;
        Assert.True(delta.Duration() <= TimeTolerance, $"{what}: expected {expected:HH:mm:ss} ±{TimeTolerance.TotalSeconds}s, got {actual:HH:mm:ss}");
    }
}
