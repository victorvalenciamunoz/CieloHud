using CieloHud.Core.Constellations;

namespace CieloHud.Core.Tests.Constellations;

/// <summary>
/// Positions are computed with our own services and turned back into a constellation. References:
/// stars belong to their Bayer constellation by definition; planets per JPL Horizons (QUANTITIES='29', Madrid,
/// 2026-10-02 21:00 UTC, queried on 2026-10-03): Saturn Cet, Jupiter Leo, Mars Cnc, Venus Vir, Moon Gem.
/// </summary>
public class AstronomyEngineConstellationLocatorTests
{
    private static readonly Observer Madrid = new(40.4168, -3.7038, 650);
    private static readonly DateTimeOffset Instant = DateTimeOffset.Parse("2026-10-02T21:00:00Z");

    private readonly AstronomyEngineConstellationLocator _locator = new();

    [Theory]
    [InlineData("Vega", "Lyr")]
    [InlineData("Betelgeuse", "Ori")]
    [InlineData("Rigel", "Ori")]
    [InlineData("Polaris", "UMi")]
    [InlineData("Sirius", "CMa")]
    [InlineData("Deneb", "Cyg")]
    [InlineData("Antares", "Sco")]
    [InlineData("Arcturus", "Boo")]
    [InlineData("Spica", "Vir")]
    [InlineData("Fomalhaut", "PsA")]
    public void Star_FallsInItsBayerConstellation(string starName, string expectedSymbol)
    {
        var position = new AstronomyEngineStarService().Locate(BrightStars.Get(starName), Madrid, Instant);

        var constellation = _locator.Locate(position.AzimuthDegrees, position.AltitudeDegrees, Madrid, Instant);

        Assert.Equal(expectedSymbol, constellation.Symbol);
    }

    [Theory]
    [InlineData(CelestialBody.Saturn, "Cet")]
    [InlineData(CelestialBody.Jupiter, "Leo")]
    [InlineData(CelestialBody.Mars, "Cnc")]
    [InlineData(CelestialBody.Venus, "Vir")]
    [InlineData(CelestialBody.Moon, "Gem")]
    public void Planet_MatchesJplHorizons(CelestialBody body, string expectedSymbol)
    {
        var position = new AstronomyEngineSolarSystemService().Locate(body, Madrid, Instant);

        var constellation = _locator.Locate(position.AzimuthDegrees, position.AltitudeDegrees, Madrid, Instant);

        Assert.Equal(expectedSymbol, constellation.Symbol);
    }

    [Fact]
    public void ReportsLatinName()
    {
        var position = new AstronomyEngineStarService().Locate(BrightStars.Get("Betelgeuse"), Madrid, Instant);

        Assert.Equal("Orion", _locator.Locate(position.AzimuthDegrees, position.AltitudeDegrees, Madrid, Instant).Name);
    }

    [Theory]
    [InlineData(0, -71)]   // hung forever inside Astronomy Engine's refraction inversion before the fix
    [InlineData(0, 90)]    // idem: pointing straight up
    [InlineData(45, 89.999)]
    [InlineData(123, -1)]
    [InlineData(200, -90)]
    [InlineData(10, 0)]
    public async Task ExtremeAltitudes_Return(double azimuth, double altitude)
    {
        var lookup = Task.Run(() => _locator.Locate(azimuth, altitude, Madrid, Instant));

        var finished = await Task.WhenAny(lookup, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(lookup, finished);
        Assert.Equal(3, (await lookup).Symbol.Length);
    }

    [Fact]
    public void WholeSphere_CoversAll88Constellations()
    {
        // Every 2° of azimuth and altitude, from a mid-northern site at two instants 12 h apart.
        var seen = new HashSet<string>();
        foreach (var instant in new[] { Instant, Instant.AddHours(12) })
            for (var alt = -89; alt <= 89; alt += 2)
                for (var az = 0; az < 360; az += 2)
                    seen.Add(_locator.Locate(az, alt, Madrid, instant).Symbol);

        Assert.Equal(88, seen.Count);
    }
}
