namespace CieloHud.Core.Tests.Cards;

public class StickyMatchTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 8, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void First_IsShownAtOnce()
    {
        Assert.Equal("Ori", new StickyMatch<string>().Update("Ori", T0));
    }

    [Fact]
    public void Another_WaitsUntilTheShownOneIsGoneForTheHold()
    {
        var sticky = new StickyMatch<string>();
        sticky.Update("Ori", T0);

        Assert.Equal("Ori", sticky.Update("Tau", T0.AddSeconds(0.5)));
        Assert.Equal("Ori", sticky.Update("Tau", T0.AddSeconds(1)));
        Assert.Equal("Tau", sticky.Update("Tau", T0.AddSeconds(1.1)));
    }

    [Fact]
    public void BackAndForthAcrossABoundary_KeepsTheShownOne()
    {
        var sticky = new StickyMatch<string>();
        sticky.Update("Ori", T0);
        for (var s = 0.2; s < 5; s += 0.4)
        {
            Assert.Equal("Ori", sticky.Update("Tau", T0.AddSeconds(s)));
            Assert.Equal("Ori", sticky.Update("Ori", T0.AddSeconds(s + 0.2)));
        }
    }

    [Fact]
    public void Nothing_ClearsAfterTheHold()
    {
        var sticky = new StickyMatch<string>();
        sticky.Update("Ori", T0);

        Assert.Equal("Ori", sticky.Update(null, T0.AddSeconds(1)));
        Assert.Null(sticky.Update(null, T0.AddSeconds(1.1)));
    }

    [Fact]
    public void Clear_ForgetsIt()
    {
        var sticky = new StickyMatch<string>();
        sticky.Update("Ori", T0);
        sticky.Clear();

        Assert.Equal("Tau", sticky.Update("Tau", T0.AddSeconds(0.1)));
    }

    [Fact]
    public void NegativeHold_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StickyMatch<string>(TimeSpan.FromSeconds(-1)));
    }
}
