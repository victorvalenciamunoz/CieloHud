namespace CieloHud.Core.Tests.Cards;

public class GalileanMoonsTests
{
    [Theory]
    [InlineData(3, 0, 5, false, GalileanMoonState.Visible)] // off the disc
    [InlineData(0.5, 0.5, 5, false, GalileanMoonState.BehindJupiter)]
    [InlineData(0.5, 0.5, -5, false, GalileanMoonState.InFrontOfJupiter)]
    [InlineData(1.5, 0, 5, true, GalileanMoonState.InJupitersShadow)] // eclipsed beside the disc
    [InlineData(0.5, 0, 5, true, GalileanMoonState.BehindJupiter)] // hidden either way: behind wins
    [InlineData(0, 0.99, 5, false, GalileanMoonState.BehindJupiter)]
    [InlineData(0, 1.01, 5, false, GalileanMoonState.Visible)] // just off the limb
    public void State_FromWhereTheMoonIs(double right, double up, double depth, bool inShadow, GalileanMoonState expected)
    {
        Assert.Equal(expected, GalileanMoons.State(right, up, depth, inShadow));
    }

    [Theory]
    [InlineData(5, 0.5, true)]
    [InlineData(5, 1.01, false)] // beside the shadow
    [InlineData(-5, 0.5, false)] // on the Sun's side
    [InlineData(26, 0.99, true)] // as far as Callisto
    public void InShadow_IsACylinderAwayFromTheSun(double along, double perpendicular, bool expected)
    {
        Assert.Equal(expected, GalileanMoons.InShadow(along, perpendicular));
    }
}
