namespace CieloHud.Core.Tests.Cards;

public class PhaseShapeTests
{
    [Theory]
    [InlineData(0.01)]
    [InlineData(0.09)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.71)]
    [InlineData(0.99)]
    [InlineData(1)]
    public void Area_IsTheLitFractionOfTheDisc(double fraction)
    {
        var outline = PhaseShape.LitOutline(fraction, brightLimbDegrees: 37, pointsPerHalf: 200);

        Assert.Equal(fraction, Area(outline) / Math.PI, 3);
    }

    [Theory]
    [InlineData(0.09, 0)]
    [InlineData(0.09, 92.8)]
    [InlineData(0.5, 200.7)]
    [InlineData(0.87, 296.8)]
    public void LitSide_FacesTheBrightLimb(double fraction, double brightLimbDegrees)
    {
        var outline = PhaseShape.LitOutline(fraction, brightLimbDegrees, pointsPerHalf: 49);

        var (right, up) = Centroid(outline);
        var direction = Math.Atan2(right, up) * 180 / Math.PI;
        Assert.Equal(0, ((direction - brightLimbDegrees) % 360 + 540) % 360 - 180, 6);
        // With an odd number of points, the middle of the bright limb is one, right towards the Sun.
        var limb = DiscPoint.Towards(brightLimbDegrees);
        Assert.Contains(outline, p => Math.Abs(p.Right - limb.Right) < 1e-9 && Math.Abs(p.Up - limb.Up) < 1e-9);
    }

    [Fact]
    public void Crescent_HornsAreSquareToTheSun()
    {
        // Bright limb to the right: the horns point straight up and down, and the limb goes round the right through (1, 0).
        var outline = PhaseShape.LitOutline(0.1, brightLimbDegrees: 90, pointsPerHalf: 5);

        Assert.Equal(8, outline.Count);
        AssertPoint(0, 1, outline[0]);
        AssertPoint(1, 0, outline[2]);
        AssertPoint(0, -1, outline[4]);
        Assert.All(outline, p => Assert.True(p.Right >= -1e-9, "a crescent stays on the Sun's side"));
    }

    [Fact]
    public void Gibbous_TerminatorBulgesIntoTheDarkSide()
    {
        var outline = PhaseShape.LitOutline(0.75, brightLimbDegrees: 90, pointsPerHalf: 5);

        // The middle of the terminator: half way from the center to the limb, on the side away from the Sun.
        AssertPoint(-0.5, 0, outline[6]);
    }

    [Fact]
    public void HalfLit_StraightTerminator()
    {
        var outline = PhaseShape.LitOutline(0.5, brightLimbDegrees: 0, pointsPerHalf: 9);

        Assert.All(outline.Skip(9), p => Assert.Equal(0, p.Up, 9));
    }

    [Fact]
    public void NothingLit_NoOutline()
    {
        Assert.Empty(PhaseShape.LitOutline(0, 90));
        Assert.Empty(PhaseShape.LitOutline(-0.1, 90));
    }

    [Fact]
    public void FullyLit_TheWholeCircle()
    {
        var outline = PhaseShape.LitOutline(1.2, 0);

        Assert.All(outline, p => Assert.Equal(1, Math.Sqrt(p.Right * p.Right + p.Up * p.Up), 9));
    }

    [Fact]
    public void TooFewPoints_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PhaseShape.LitOutline(0.5, 0, pointsPerHalf: 1));
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(90, 1, 0)]
    [InlineData(180, 0, -1)]
    [InlineData(270, -1, 0)]
    public void Towards_ZeroUpNinetyRight(double degrees, double right, double up)
    {
        var point = DiscPoint.Towards(degrees);

        Assert.Equal(right, point.Right, 9);
        Assert.Equal(up, point.Up, 9);
    }

    private static void AssertPoint(double right, double up, DiscPoint point)
    {
        Assert.Equal(right, point.Right, 9);
        Assert.Equal(up, point.Up, 9);
    }

    /// <summary>Shoelace formula; the outline goes clockwise or counterclockwise depending on the side, so take the size.</summary>
    private static double Area(IReadOnlyList<DiscPoint> outline)
    {
        double sum = 0;
        for (var i = 0; i < outline.Count; i++)
        {
            var (a, b) = (outline[i], outline[(i + 1) % outline.Count]);
            sum += a.Right * b.Up - b.Right * a.Up;
        }
        return Math.Abs(sum) / 2;
    }

    private static (double Right, double Up) Centroid(IReadOnlyList<DiscPoint> outline)
    {
        double sum = 0, right = 0, up = 0;
        for (var i = 0; i < outline.Count; i++)
        {
            var (a, b) = (outline[i], outline[(i + 1) % outline.Count]);
            var cross = a.Right * b.Up - b.Right * a.Up;
            sum += cross;
            right += (a.Right + b.Right) * cross;
            up += (a.Up + b.Up) * cross;
        }
        return (right / (3 * sum), up / (3 * sum));
    }
}
