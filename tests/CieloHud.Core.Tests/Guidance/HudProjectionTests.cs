namespace CieloHud.Core.Tests.Guidance;

public class HudProjectionTests
{
    private const double W = 600, H = 1200;

    // Pixels per unit of tangent with a 60° horizontal field of view: (W/2) / tan(30°).
    private static readonly double K = W / 2 / Math.Tan(Math.PI / 6);

    private static double Tan(double degrees) => Math.Tan(degrees * Math.PI / 180);

    [Fact]
    public void PointingAtTheTarget_IsTheCenter()
    {
        var p = HudProjection.Project(new PointingDirection(120, 35), 120, 35, W, H);

        Assert.Equal(300, p.X, 1e-9);
        Assert.Equal(600, p.Y, 1e-9);
        Assert.True(p.InView);
    }

    [Fact]
    public void TargetToTheRight_MovesRightByTheTangent()
    {
        var p = HudProjection.ToScreen(new PointingDirection(0, 0), 10, 0, W, H);

        Assert.NotNull(p);
        Assert.Equal(300 + K * Tan(10), p.Value.X, 1e-6);
        Assert.Equal(600, p.Value.Y, 1e-6);
    }

    [Fact]
    public void TargetAbove_MovesUpByTheTangent()
    {
        var p = HudProjection.ToScreen(new PointingDirection(0, 0), 0, 20, W, H);

        Assert.Equal(300, p!.Value.X, 1e-6);
        Assert.Equal(600 - K * Tan(20), p.Value.Y, 1e-6);
    }

    [Fact]
    public void NearTheZenith_TargetOverTheTop_IsStraightUp()
    {
        // Pointing north at 80°; the target is north... over the zenith, 20° away along the meridian.
        var p = HudProjection.ToScreen(new PointingDirection(0, 80), 180, 80, W, H);

        Assert.Equal(300, p!.Value.X, 1e-6);
        Assert.Equal(600 - K * Tan(20), p.Value.Y, 1e-6);
    }

    [Fact]
    public void GreatCircle_ProjectsAsAStraightLine()
    {
        // Three points on the horizon seen while looking 30° up: a gnomonic projection keeps them collinear.
        var pointing = new PointingDirection(90, 30);
        var a = HudProjection.ToScreen(pointing, 60, 0, W, H)!.Value;
        var b = HudProjection.ToScreen(pointing, 90, 0, W, H)!.Value;
        var c = HudProjection.ToScreen(pointing, 125, 0, W, H)!.Value;

        var cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        Assert.Equal(0, cross, 1e-6);
        Assert.Equal(a.Y, b.Y, 1e-6); // and level, because the phone is upright
    }

    [Fact]
    public void BehindTheViewer_HasNoScreenPosition()
    {
        Assert.Null(HudProjection.ToScreen(new PointingDirection(0, 0), 180, 0, W, H));
        Assert.Null(HudProjection.ToScreen(new PointingDirection(0, 0), 90, 0, W, H)); // exactly 90° away
    }

    [Fact]
    public void OffScreenToTheRight_IsClampedToTheEdge()
    {
        var p = HudProjection.Project(new PointingDirection(0, 0), 50, 0, W, H, edgeMargin: 48);

        Assert.False(p.InView);
        Assert.Equal(552, p.X, 1e-6);
        Assert.Equal(600, p.Y, 1e-6);
        Assert.Equal(0, p.DirectionDegrees, 1e-6);
    }

    [Fact]
    public void BehindAndToTheRight_ArrowPointsRight()
    {
        var p = HudProjection.Project(new PointingDirection(0, 0), 135, 0, W, H, edgeMargin: 48);

        Assert.False(p.InView);
        Assert.Equal(552, p.X, 1e-6);
        Assert.Equal(0, p.DirectionDegrees, 1e-6);
    }

    [Fact]
    public void BehindAndAbove_ArrowPointsUp()
    {
        var p = HudProjection.Project(new PointingDirection(0, 0), 180, 60, W, H, edgeMargin: 0);

        Assert.False(p.InView);
        Assert.Equal(90, p.DirectionDegrees, 1e-6);
        Assert.Equal(0, p.Y, 1e-6);
    }

    [Fact]
    public void ExactlyBehind_StillGetsAnEdgePoint()
    {
        var p = HudProjection.Project(new PointingDirection(0, 0), 180, 0, W, H, edgeMargin: 48);

        Assert.False(p.InView);
        Assert.Equal(552, p.X, 1e-6);
    }

    [Fact]
    public void Diagonal_ClampsOnTheNearestSide()
    {
        var p = HudProjection.Project(new PointingDirection(0, 0), -60, -60, W, H, edgeMargin: 0);

        Assert.False(p.InView);
        Assert.Equal(0, p.X, 1e-6); // left edge reached first (narrower half-width)
        Assert.True(p.Y > 600);
        Assert.InRange(p.DirectionDegrees, -180, -90);
    }

    [Fact]
    public void BadInputs_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HudProjection.ToScreen(new PointingDirection(0, 0), 0, 0, 0, H));
        Assert.Throws<ArgumentOutOfRangeException>(() => HudProjection.Project(new PointingDirection(0, 0), 0, 0, W, H, fieldOfViewDegrees: 0));
    }
}
