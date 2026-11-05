namespace CieloHud.Core.Tests.Satellites;

public class Sgp4SatelliteIlluminationServiceTests
{
    private readonly Sgp4SatelliteIlluminationService _service = new();

    [Theory]
    [InlineData("2026-10-02T11:26:00Z")] // over Madrid at local noon
    [InlineData("2026-10-02T17:55:45Z")] // evening pass over Madrid, Sun at -0.9° for the observer, ISS 400 km up still lit
    public void IsSunlit_OverMadridByDay_IsTrue(string instant)
    {
        Assert.True(_service.IsSunlit(TleTests.Iss, DateTimeOffset.Parse(instant)));
    }

    [Theory]
    [InlineData("2026-10-02T00:00:00Z")]
    [InlineData("2026-10-02T12:00:00Z")]
    public void IsSunlit_OverOneOrbit_HasOneEclipseOfPlausibleLength(string orbitStart)
    {
        // Independent physical check: in October the ISS (51.6° inclination, ~93 min period) is eclipsed once per orbit
        // for roughly a third of it. The exact fraction depends on the beta angle, hence the wide range.
        var start = DateTimeOffset.Parse(orbitStart);
        var period = TimeSpan.FromMinutes(1440.0 / 15.48706258);
        var step = TimeSpan.FromSeconds(10);

        int darkSamples = 0, totalSamples = 0, transitions = 0;
        bool? previous = null;
        for (var t = start; t < start + period; t += step)
        {
            var lit = _service.IsSunlit(TleTests.Iss, t);
            totalSamples++;
            if (!lit) darkSamples++;
            if (previous is { } p && p != lit) transitions++;
            previous = lit;
        }

        var darkFraction = (double)darkSamples / totalSamples;
        Assert.InRange(darkFraction, 0.20, 0.45);
        Assert.Equal(2, transitions); // one entry into shadow, one exit
    }

    [Fact]
    public void IsSunlit_IgnoresLocalOffset_UsesInstant()
    {
        var utc = _service.IsSunlit(TleTests.Iss, DateTimeOffset.Parse("2026-10-02T11:26:00Z"));
        var local = _service.IsSunlit(TleTests.Iss, DateTimeOffset.Parse("2026-10-02T13:26:00+02:00"));

        Assert.Equal(utc, local);
    }
}
