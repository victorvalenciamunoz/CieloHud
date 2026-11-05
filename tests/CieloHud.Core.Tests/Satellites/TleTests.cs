namespace CieloHud.Core.Tests.Satellites;

public class TleTests
{
    // ISS elements downloaded from CelesTrak on 2026-10-02. Kept fixed so tests never hit the network.
    public const string IssName = "ISS (ZARYA)";
    public const string IssLine1 = "1 25544U 98067A   26274.82022115  .00003852  00000+0  78825-4 0  9993";
    public const string IssLine2 = "2 25544  51.6316 132.3693 0006937 211.2109 148.8469 15.48706258588289";

    public static Tle Iss => new(IssName, IssLine1, IssLine2);

    [Fact]
    public void Constructor_ParsesNoradNumberAndEpoch()
    {
        var tle = Iss;

        Assert.Equal(25544, tle.NoradNumber);
        // Day 274.82022115 of 2026 = 2026-10-01 19:41:07.1 UTC.
        var expectedEpoch = new DateTimeOffset(2026, 10, 1, 19, 41, 7, TimeSpan.Zero);
        Assert.InRange((tle.Epoch - expectedEpoch).TotalSeconds, -1, 1);
        Assert.Equal(TimeSpan.Zero, tle.Epoch.Offset);
    }

    [Fact]
    public void Constructor_TrimsName()
    {
        var tle = new Tle("ISS (ZARYA)             ", IssLine1, IssLine2);

        Assert.Equal("ISS (ZARYA)", tle.Name);
    }

    [Fact]
    public void Parse_AcceptsCelesTrakThreeLineFormat()
    {
        var text = $"{IssName}             \r\n{IssLine1}\r\n{IssLine2}\r\n";

        var tle = Tle.Parse(text);

        Assert.Equal(Iss, tle);
    }

    [Fact]
    public void Parse_RejectsWrongLineCount()
    {
        Assert.Throws<FormatException>(() => Tle.Parse($"{IssLine1}\n{IssLine2}"));
    }

    [Theory]
    [InlineData("1 25544U 98067A   26274.82022115  .00003852  00000+0  78825-4 0  999")]  // 68 chars
    [InlineData("2 25544U 98067A   26274.82022115  .00003852  00000+0  78825-4 0  9993")] // wrong line number
    public void Constructor_RejectsMalformedLine1(string line1)
    {
        Assert.Throws<FormatException>(() => new Tle(IssName, line1, IssLine2));
    }

    [Fact]
    public void Epoch_UsesTwoDigitYearPivotAt57()
    {
        var line1From1999 = "1 25544U 98067A   99274.82022115  .00003852  00000+0  78825-4 0  9993";

        var tle = new Tle(IssName, line1From1999, IssLine2);

        Assert.Equal(1999, tle.Epoch.Year);
    }
}
