namespace CieloHud.Core.Tests.Satellites;

public class FixedTleProviderTests
{
    [Fact]
    public async Task GetTleAsync_ReturnsConfiguredTle()
    {
        var provider = new FixedTleProvider(TleTests.Iss);

        var tle = await provider.GetTleAsync(25544);

        Assert.Equal(TleTests.Iss, tle);
    }

    [Fact]
    public async Task GetTleAsync_UnknownNorad_Throws()
    {
        var provider = new FixedTleProvider(TleTests.Iss);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => provider.GetTleAsync(1));
    }
}
