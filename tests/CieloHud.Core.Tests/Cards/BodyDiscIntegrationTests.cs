namespace CieloHud.Core.Tests.Cards;

/// <summary>
/// Reference values from JPL Horizons (https://ssd.jpl.nasa.gov/api/horizons.api), queried on 2026-10-07: observer tables from
/// Madrid (CENTER='coord@399', SITE_COORD='-3.7038,40.4168,0.650'), QUANTITIES='2,4,10,17,27,42', ANG_FORMAT='DEG':
/// apparent declination, lit percentage, the north pole's position angle (NP.ang), the position angle of the Sun-to-target
/// radius vector (PsAng, pointing away from the Sun: the bright limb is the opposite way) and the local hour angle.
/// Position angles count from celestial north through east, counterclockwise on the sky; they are turned to the HUD's axes
/// (from the zenith, clockwise) with the parallactic angle q, the position angle of the zenith: HUD = q − PA.
/// </summary>
public class BodyDiscIntegrationTests
{
    // Measured: lit fraction within 0.01 points, bright limb within 0.17°, north pole within 0.01°.
    private const double LitPercentTolerance = 0.05;
    private const double AngleToleranceDegrees = 0.5;

    private static readonly Observer Madrid = new(latitudeDegrees: 40.4168, longitudeDegrees: -3.7038, altitudeMeters: 650);

    private readonly AstronomyEngineSolarSystemFactsService _service = new();

    [Theory]
    [InlineData(CelestialBody.Moon, "2026-10-08T05:00:00Z", 4.08364, 6.41330, 21.2847, 289.696, -5.203323958)]
    [InlineData(CelestialBody.Moon, "2026-10-14T18:30:00Z", -27.44666, 16.15995, 9.7991, 105.017, 3.429148533)]
    [InlineData(CelestialBody.Moon, "2026-10-22T21:00:00Z", -4.12177, 86.99965, 338.5771, 68.303, -0.342898000)]
    [InlineData(CelestialBody.Moon, "2026-10-29T03:00:00Z", 27.21366, 88.16429, 353.2101, 258.166, 0.312507191)]
    [InlineData(CelestialBody.Venus, "2026-10-07T12:00:00Z", -21.30593, 9.32073, 19.7860, 126.103, -1.382718516)]
    [InlineData(CelestialBody.Venus, "2026-10-07T17:00:00Z", -21.30128, 9.14175, 19.7962, 126.315, 3.634033778)]
    [InlineData(CelestialBody.Mercury, "2026-10-07T13:00:00Z", -16.79027, 69.29866, 25.8294, 114.357, -0.543160289)]
    [InlineData(CelestialBody.Mars, "2026-10-08T05:00:00Z", 19.91735, 90.15449, 354.1367, 284.652, -2.690736691)]
    [InlineData(CelestialBody.Jupiter, "2026-10-08T05:00:00Z", 15.13009, 99.41508, 20.7505, 288.986, -3.686286729)]
    [InlineData(CelestialBody.Saturn, "2026-10-07T21:00:00Z", 1.86950, 99.99829, 3.2519, 29.855, -2.892879340)]
    [InlineData(CelestialBody.Saturn, "2026-10-08T01:30:00Z", 1.86377, 99.99816, 3.2531, 31.367, 1.620358820)]
    public void Disc_MatchesHorizonsFromMadrid(
        CelestialBody body, string instant, double declination, double litPercent, double northPoleAngle, double sunTargetAngle, double hourAngleHours)
    {
        var disc = _service.Disc(body, Madrid, DateTimeOffset.Parse(instant));

        var q = ParallacticAngle(hourAngleHours * 15, declination, Madrid.LatitudeDegrees);
        Assert.InRange(disc.IlluminatedFraction * 100, litPercent - LitPercentTolerance, litPercent + LitPercentTolerance);
        AssertAngle(q - (sunTargetAngle + 180), disc.BrightLimbDegrees);
        AssertAngle(q - northPoleAngle, disc.NorthPoleDegrees);
    }

    [Theory]
    [InlineData("2026-10-08T05:00:00Z")]
    [InlineData("2026-10-22T21:00:00Z")]
    public void Moon_SameLitFractionAsItsFacts(string instant)
    {
        var at = DateTimeOffset.Parse(instant);

        Assert.Equal(_service.Moon(Madrid, at).IlluminatedFraction, _service.Disc(CelestialBody.Moon, Madrid, at).IlluminatedFraction);
    }

    /// <summary>Position angle of the zenith seen from the body: tan q = sin H / (tan φ cos δ − sin δ cos H).</summary>
    private static double ParallacticAngle(double hourAngleDegrees, double declinationDegrees, double latitudeDegrees)
    {
        var (h, d, phi) = (Radians(hourAngleDegrees), Radians(declinationDegrees), Radians(latitudeDegrees));
        return Math.Atan2(Math.Sin(h), Math.Tan(phi) * Math.Cos(d) - Math.Sin(d) * Math.Cos(h)) * 180 / Math.PI;
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180;

    private static void AssertAngle(double expected, double actual)
    {
        var difference = ((actual - expected) % 360 + 540) % 360 - 180;
        Assert.InRange(difference, -AngleToleranceDegrees, AngleToleranceDegrees);
        Assert.InRange(actual, 0, 360);
    }
}
