namespace CieloHud.Core.Tests.Cards;

/// <summary>
/// Reference values from JPL Horizons (https://ssd.jpl.nasa.gov/api/horizons.api), queried on 2026-10-07:
/// <list type="bullet">
/// <item>Offsets: airless apparent azimuth and elevation (QUANTITIES='4,13') of Jupiter (599) and of Io, Europa, Ganymede and
/// Callisto (501-504) from Madrid (CENTER='coord@399', SITE_COORD='-3.7038,40.4168,0.650'), projected exactly on the plane of
/// the sky around Jupiter (right = towards increasing azimuth, up = towards the zenith) and divided by Jupiter's apparent
/// equatorial radius (half the 'Ang-diam' of quantity 13).</item>
/// <item>Events: the same, minute by minute, with the moon's distance (quantity 20) for in front or behind, and for the shadow
/// the moon and the Sun as vectors from Jupiter (EPHEM_TYPE='VECTORS', CENTER='500@599', TIME_TYPE='UT') when the light left
/// Jupiter. The same rules (<see cref="GalileanMoons"/>) applied to Horizons' positions give the minute each state starts.</item>
/// </list>
/// </summary>
public class JupiterMoonsIntegrationTests
{
    // Measured: offsets within 0.02 Jupiter radii (0.35"), Jupiter's apparent radius within 0.1 %, events within a minute.
    private const double OffsetToleranceRadii = 0.05;
    private static readonly TimeSpan EventTolerance = TimeSpan.FromMinutes(2);

    private static readonly Observer Madrid = new(latitudeDegrees: 40.4168, longitudeDegrees: -3.7038, altitudeMeters: 650);

    private readonly AstronomyEngineSolarSystemFactsService _service = new();

    [Theory]
    [InlineData("2026-10-08T04:00:00Z", GalileanMoonName.Io, 1.2217, 3.9672)]
    [InlineData("2026-10-08T04:00:00Z", GalileanMoonName.Europa, -2.7516, -8.8416)]
    [InlineData("2026-10-08T04:00:00Z", GalileanMoonName.Ganymede, 4.2809, 13.8991)]
    [InlineData("2026-10-08T04:00:00Z", GalileanMoonName.Callisto, 6.0960, 19.4760)]
    [InlineData("2026-10-08T05:30:00Z", GalileanMoonName.Io, 1.0842, 2.9377)]
    [InlineData("2026-10-08T05:30:00Z", GalileanMoonName.Europa, -3.1867, -8.5156)]
    [InlineData("2026-10-08T05:30:00Z", GalileanMoonName.Ganymede, 5.1029, 13.7989)]
    [InlineData("2026-10-08T05:30:00Z", GalileanMoonName.Callisto, 7.3031, 19.4648)]
    [InlineData("2026-11-16T06:25:00Z", GalileanMoonName.Io, 1.2160, 0.4852)]
    [InlineData("2026-11-16T06:25:00Z", GalileanMoonName.Europa, -8.5922, -3.5862)]
    [InlineData("2026-11-16T06:25:00Z", GalileanMoonName.Ganymede, -11.7931, -5.0200)]
    [InlineData("2026-11-16T06:25:00Z", GalileanMoonName.Callisto, 4.1491, 1.5471)]
    public void Offsets_MatchHorizonsFromMadrid(string instant, GalileanMoonName name, double right, double up)
    {
        var moon = _service.JupiterMoons(Madrid, DateTimeOffset.Parse(instant)).Moons.Single(m => m.Name == name);

        Assert.InRange(moon.RightRadii, right - OffsetToleranceRadii, right + OffsetToleranceRadii);
        Assert.InRange(moon.UpRadii, up - OffsetToleranceRadii, up + OffsetToleranceRadii);
    }

    [Theory]
    [InlineData("2026-10-08T04:00:00Z", 16.8972)]
    [InlineData("2026-11-16T06:25:00Z", 18.7288)]
    public void JupiterRadius_MatchesHorizons(string instant, double arcseconds)
    {
        var radius = _service.JupiterMoons(Madrid, DateTimeOffset.Parse(instant)).JupiterRadiusArcseconds;

        Assert.InRange(radius, arcseconds * 0.995, arcseconds * 1.005);
    }

    /// <summary>The minute each state starts according to Horizons; CieloHud must agree two minutes either side.</summary>
    [Theory]
    [InlineData("2026-10-08T07:07:00Z", GalileanMoonName.Io, GalileanMoonState.Visible, GalileanMoonState.InJupitersShadow)]
    [InlineData("2026-10-08T08:09:00Z", GalileanMoonName.Io, GalileanMoonState.InJupitersShadow, GalileanMoonState.BehindJupiter)]
    [InlineData("2026-10-08T10:28:00Z", GalileanMoonName.Io, GalileanMoonState.BehindJupiter, GalileanMoonState.Visible)]
    [InlineData("2026-10-08T22:19:00Z", GalileanMoonName.Europa, GalileanMoonState.Visible, GalileanMoonState.InFrontOfJupiter)]
    [InlineData("2026-10-09T01:13:00Z", GalileanMoonName.Europa, GalileanMoonState.InFrontOfJupiter, GalileanMoonState.Visible)]
    [InlineData("2026-10-09T23:25:00Z", GalileanMoonName.Ganymede, GalileanMoonState.Visible, GalileanMoonState.InJupitersShadow)]
    [InlineData("2026-10-10T03:04:00Z", GalileanMoonName.Ganymede, GalileanMoonState.InJupitersShadow, GalileanMoonState.Visible)]
    [InlineData("2026-10-13T14:50:00Z", GalileanMoonName.Callisto, GalileanMoonState.Visible, GalileanMoonState.InJupitersShadow)]
    public void Events_MatchHorizonsToTheMinute(string startsAt, GalileanMoonName name, GalileanMoonState before, GalileanMoonState after)
    {
        var at = DateTimeOffset.Parse(startsAt);

        Assert.Equal(before, StateOf(name, at - EventTolerance));
        Assert.Equal(after, StateOf(name, at + EventTolerance));
    }

    [Fact]
    public void Moons_InOrderFromIoToCallisto()
    {
        var moons = _service.JupiterMoons(Madrid, DateTimeOffset.Parse("2026-10-08T04:00:00Z")).Moons;

        Assert.Equal(Enum.GetValues<GalileanMoonName>(), moons.Select(m => m.Name));
    }

    private GalileanMoonState StateOf(GalileanMoonName name, DateTimeOffset instant) =>
        _service.JupiterMoons(Madrid, instant).Moons.Single(m => m.Name == name).State;
}
