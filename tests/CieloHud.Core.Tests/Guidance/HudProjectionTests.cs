using GuidanceResult = CieloHud.Core.Guidance.Guidance;

namespace CieloHud.Core.Tests.Guidance;

public class HudProjectionTests
{
    private const double W = 600, H = 1200; // 10 px per degree with a 60° field of view

    private static GuidanceResult G(double azimuthDelta, double altitudeDelta) => new(azimuthDelta, altitudeDelta, 0, false);

    [Fact]
    public void OnTarget_IsAtTheCenter()
    {
        var p = HudProjection.Project(G(0, 0), 0, W, H);

        Assert.Equal(300, p.X);
        Assert.Equal(600, p.Y);
        Assert.True(p.InView);
    }

    [Fact]
    public void TargetToTheRight_MovesRight()
    {
        var p = HudProjection.Project(G(10, 0), 0, W, H);

        Assert.Equal(400, p.X, 1e-9);
        Assert.Equal(600, p.Y, 1e-9);
        Assert.Equal(0, p.DirectionDegrees, 1e-9);
        Assert.True(p.InView);
    }

    [Fact]
    public void TargetAbove_MovesUp()
    {
        var p = HudProjection.Project(G(0, 20), 0, W, H);

        Assert.Equal(300, p.X, 1e-9);
        Assert.Equal(400, p.Y, 1e-9);
        Assert.Equal(90, p.DirectionDegrees, 1e-9);
    }

    [Fact]
    public void AzimuthOffset_ShrinksWithPointingAltitude()
    {
        var horizon = HudProjection.Project(G(10, 0), 0, W, H);
        var high = HudProjection.Project(G(10, 0), 60, W, H);

        Assert.Equal(100, horizon.X - 300, 1e-9);
        Assert.Equal(50, high.X - 300, 1e-6); // cos 60° = 0.5
    }

    [Fact]
    public void FarTarget_IsClampedToTheEdge_KeepingDirection()
    {
        var p = HudProjection.Project(G(90, 0), 0, W, H, edgeMargin: 50);

        Assert.False(p.InView);
        Assert.Equal(550, p.X, 1e-9); // half width 300 minus margin 50
        Assert.Equal(600, p.Y, 1e-9);
        Assert.Equal(0, p.DirectionDegrees, 1e-9);
    }

    [Fact]
    public void FarTarget_Diagonal_IsClampedOnTheNearestSide()
    {
        var p = HudProjection.Project(G(-90, -90), 0, W, H, edgeMargin: 0);

        Assert.False(p.InView);
        Assert.Equal(0, p.X, 1e-9);     // left edge limits first (narrower)
        Assert.Equal(900, p.Y, 1e-9);   // same scale applied to dy: 300 px down from center
        Assert.Equal(-135, p.DirectionDegrees, 1e-9);
    }

    [Fact]
    public void JustInsideMargin_IsInView()
    {
        var p = HudProjection.Project(G(24, 0), 0, W, H, edgeMargin: 50);

        Assert.True(p.InView);
    }

    [Fact]
    public void BadInputs_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HudProjection.Project(G(0, 0), 0, 0, H));
        Assert.Throws<ArgumentOutOfRangeException>(() => HudProjection.Project(G(0, 0), 0, W, H, fieldOfViewDegrees: 0));
    }
}
