namespace CieloHud.Core.Tests.Cards;

public class RecentMatchTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 7, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Recognized_IsOffered()
    {
        Assert.Equal("Júpiter", new RecentMatch<string>().Update("Júpiter", T0));
    }

    [Fact]
    public void Lost_IsStillOfferedDuringTheGrace()
    {
        var match = new RecentMatch<string>();
        match.Update("Júpiter", T0);

        Assert.Equal("Júpiter", match.Update(null, T0.AddSeconds(1)));
        Assert.Equal("Júpiter", match.Update(null, T0.AddSeconds(2)));
        Assert.Null(match.Update(null, T0.AddSeconds(2.1)));
    }

    [Fact]
    public void SeenAgain_RestartsTheGrace()
    {
        var match = new RecentMatch<string>();
        match.Update("Júpiter", T0);
        match.Update(null, T0.AddSeconds(1.5));
        match.Update("Júpiter", T0.AddSeconds(1.8));

        Assert.Equal("Júpiter", match.Update(null, T0.AddSeconds(3.5)));
    }

    [Fact]
    public void AnotherObject_ReplacesItAtOnce()
    {
        var match = new RecentMatch<string>();
        match.Update("Júpiter", T0);

        Assert.Equal("Luna", match.Update("Luna", T0.AddSeconds(0.5)));
        Assert.Equal("Luna", match.Update(null, T0.AddSeconds(1)));
    }

    [Fact]
    public void Clear_ForgetsIt()
    {
        var match = new RecentMatch<string>();
        match.Update("Júpiter", T0);

        match.Clear();

        Assert.Null(match.Update(null, T0.AddSeconds(0.1)));
    }

    [Fact]
    public void NothingEverRecognized_OffersNothing()
    {
        Assert.Null(new RecentMatch<string>().Update(null, T0));
    }

    [Fact]
    public void NegativeGrace_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecentMatch<string>(TimeSpan.FromSeconds(-1)));
    }
}
