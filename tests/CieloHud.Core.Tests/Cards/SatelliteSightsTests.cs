namespace CieloHud.Core.Tests.Cards;

public class SatelliteSightsTests
{
    private static readonly VisibilityCriteria Criteria = VisibilityCriteria.Default;

    [Fact]
    public void LitWithADarkSky_Visible() =>
        Assert.Equal(SatelliteSight.Visible, SatelliteSights.Of(altitudeDegrees: 30, sunlit: true, sunAltitudeDegrees: -12, Criteria));

    [Theory]
    [InlineData(20)]
    [InlineData(-3)]
    [InlineData(-6)]
    public void LitWithTheSunAboveMinusSix_SkyTooBright(double sunAltitude) =>
        Assert.Equal(SatelliteSight.SkyTooBright, SatelliteSights.Of(30, sunlit: true, sunAltitude, Criteria));

    [Theory]
    [InlineData(-12)]
    [InlineData(20)]
    public void InTheShadow_NeverSeenWhateverTheSky(double sunAltitude) =>
        Assert.Equal(SatelliteSight.InEarthShadow, SatelliteSights.Of(30, sunlit: false, sunAltitude, Criteria));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BelowTheHorizon_NothingElseMatters(bool sunlit) =>
        Assert.Equal(SatelliteSight.BelowHorizon, SatelliteSights.Of(-0.5, sunlit, -12, Criteria));

    [Fact]
    public void FollowsTheCriteria()
    {
        var strict = new VisibilityCriteria { MaxSunAltitudeDegrees = -12 };

        Assert.Equal(SatelliteSight.SkyTooBright, SatelliteSights.Of(30, sunlit: true, sunAltitudeDegrees: -8, strict));
    }
}
