using CieloHud.Core.Stars;

namespace CieloHud.Core.Tests.Stars;

/// <summary>
/// Horizons does not compute stars, so these tests use geometry that does not depend on our code:
/// Polaris stands at an altitude close to the observer's latitude, and every star culminates due south (or north)
/// at 90° − latitude + declination.
/// </summary>
public class AstronomyEngineStarServiceTests
{
    private static readonly Observer Madrid = new(40.4168, -3.7038, 650);
    private readonly AstronomyEngineStarService _service = new();

    [Theory]
    [InlineData("2026-10-03T00:00:00Z")]
    [InlineData("2026-10-03T06:00:00Z")]
    [InlineData("2026-10-03T12:00:00Z")]
    [InlineData("2026-10-03T18:00:00Z")]
    public void Polaris_StaysNearTheNorthCelestialPole(string instant)
    {
        var polaris = _service.Locate(BrightStars.Get("Polaris"), Madrid, DateTimeOffset.Parse(instant));

        // Polaris is 0.64° from the pole in 2026, plus ~0.02° of refraction at 40°.
        Assert.InRange(polaris.AltitudeDegrees, Madrid.LatitudeDegrees - 0.75, Madrid.LatitudeDegrees + 0.75);
        Assert.InRange(GuidanceCalculator.WrapToHalfTurn(polaris.AzimuthDegrees), -1.2, 1.2);
        Assert.Null(polaris.DistanceKm);
    }

    [Theory]
    [InlineData("Vega")]      // culminates 1.6° from the zenith, south
    [InlineData("Sirius")]
    [InlineData("Arcturus")]
    [InlineData("Antares")]
    [InlineData("Deneb")]     // dec > latitude: culminates north of the zenith
    [InlineData("Capella")]   // idem
    public void Culmination_MatchesLatitudeAndDeclination(string name)
    {
        var star = BrightStars.Get(name);

        // Highest point over one sidereal day: every minute, then every second around the best minute
        // (near the zenith the azimuth swings several degrees per minute).
        var start = DateTimeOffset.Parse("2026-10-03T00:00:00Z");
        var bestTime = start;
        var best = new HorizontalPosition(0, -90);
        for (var minute = 0; minute < 1437; minute++)
        {
            var t = start.AddMinutes(minute);
            var p = _service.Locate(star, Madrid, t);
            if (p.AltitudeDegrees > best.AltitudeDegrees) { best = p; bestTime = t; }
        }
        var coarse = bestTime;
        for (var second = -60; second <= 60; second++)
        {
            var t = coarse.AddSeconds(second);
            var p = _service.Locate(star, Madrid, t);
            if (p.AltitudeDegrees > best.AltitudeDegrees) best = p;
        }

        // Precession since J2000 moves declinations by up to ~0.15°; refraction adds up to ~0.1° at these altitudes.
        var expected = 90 - Math.Abs(Madrid.LatitudeDegrees - star.DeclinationDegrees);
        Assert.InRange(best.AltitudeDegrees, expected - 0.3, expected + 0.4);

        // Upper culmination is due south when the star's declination is below the observer's latitude, due north otherwise.
        var expectedAzimuth = star.DeclinationDegrees < Madrid.LatitudeDegrees ? 180 : 0;
        Assert.InRange(GuidanceCalculator.WrapToHalfTurn(best.AzimuthDegrees - expectedAzimuth), -1, 1);
    }

    [Fact]
    public void Locate_IgnoresLocalOffset_UsesInstant()
    {
        var vega = BrightStars.Get("Vega");

        var utc = _service.Locate(vega, Madrid, DateTimeOffset.Parse("2026-10-02T21:00:00Z"));
        var local = _service.Locate(vega, Madrid, DateTimeOffset.Parse("2026-10-02T23:00:00+02:00"));

        Assert.Equal(utc, local);
    }

    [Fact]
    public void Catalog_HasUniqueNamesAndPlausibleData()
    {
        Assert.Equal(155, BrightStars.All.Count);
        Assert.Equal(BrightStars.All.Count, BrightStars.All.Select(s => s.Designation).Distinct().Count());
        var named = BrightStars.All.Where(s => s.ProperName is not null).ToList();
        Assert.Equal(named.Count, named.Select(s => s.ProperName).Distinct().Count());
        foreach (var star in BrightStars.All)
        {
            Assert.InRange(star.RightAscensionDegrees, 0, 360);
            Assert.InRange(star.DeclinationDegrees, -50, 90);
            Assert.InRange(star.Magnitude, -1.5, 3.05);
        }
        Assert.Equal(BrightStars.All.OrderBy(s => s.Magnitude), BrightStars.All);
    }

    [Fact]
    public void Catalog_NoTwoStarsAtTheSamePlace()
    {
        // System rows and their components (bet Sco / bet01 Sco) must have collapsed into one entry.
        var stars = BrightStars.All;
        for (var i = 0; i < stars.Count; i++)
            for (var j = i + 1; j < stars.Count; j++)
            {
                var separation = GuidanceCalculator.AngularDistance(
                    stars[i].RightAscensionDegrees, stars[i].DeclinationDegrees,
                    stars[j].RightAscensionDegrees, stars[j].DeclinationDegrees);
                Assert.True(separation > 0.01, $"{stars[i].Designation} and {stars[j].Designation} are {separation:F4}° apart");
            }
    }

    [Theory]
    [InlineData("Alnitak", "zet Ori")]
    [InlineData("Dubhe", "alf UMa")]
    [InlineData("gam Cas", "gam Cas")] // no IAU proper name: found by designation, named by it
    public void Get_FindsByProperNameOrDesignation(string query, string expectedDesignation)
    {
        Assert.Equal(expectedDesignation, BrightStars.Get(query).Designation);
    }

    [Fact]
    public void UnnamedStar_UsesDesignationAsName()
    {
        var gammaCas = BrightStars.Get("gam Cas");

        Assert.Null(gammaCas.ProperName);
        Assert.Equal("gam Cas", gammaCas.Name);
    }

    [Fact]
    public void Get_UnknownStar_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => BrightStars.Get("Krypton"));
    }
}
