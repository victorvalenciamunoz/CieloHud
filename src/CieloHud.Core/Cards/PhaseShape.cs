namespace CieloHud.Core.Cards;

/// <summary>
/// The lit part of a disc of radius 1, for the drawing of the Moon and the planets (decision 041). It is bounded by half the
/// limb, on the bright side, and the terminator: half an ellipse with semi-axis |1 − 2f| across the line to the Sun, on the
/// dark side past half lit. Its area is the lit fraction times π.
/// </summary>
public static class PhaseShape
{
    /// <summary>
    /// The outline, closed implicitly, in <see cref="BodyDisc"/>'s axes: the bright limb from one horn to the other through
    /// <paramref name="brightLimbDegrees"/>, then the terminator back. Empty when nothing is lit.
    /// </summary>
    /// <param name="fraction">Lit fraction, 0 to 1.</param>
    /// <param name="pointsPerHalf">Points on each half, the limb and the terminator, counting the horns they share.</param>
    public static IReadOnlyList<DiscPoint> LitOutline(double fraction, double brightLimbDegrees, int pointsPerHalf = 48)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pointsPerHalf, 2);
        fraction = Math.Clamp(fraction, 0, 1);
        if (fraction == 0)
            return [];

        // Local frame: "along" towards the Sun, "across" 90° clockwise from it on the sky.
        var along = DiscPoint.Towards(brightLimbDegrees);
        var across = DiscPoint.Towards(brightLimbDegrees + 90);
        DiscPoint At(double a, double c) => new(a * along.Right + c * across.Right, a * along.Up + c * across.Up);

        var terminator = 1 - 2 * fraction;
        var outline = new List<DiscPoint>(2 * pointsPerHalf - 2);
        for (var i = 0; i < pointsPerHalf; i++)
        {
            var angle = Math.PI * (-0.5 + (double)i / (pointsPerHalf - 1));
            outline.Add(At(Math.Cos(angle), Math.Sin(angle)));
        }
        // The horns are shared: the terminator only adds the points between them.
        for (var i = 1; i < pointsPerHalf - 1; i++)
        {
            var angle = Math.PI * (0.5 - (double)i / (pointsPerHalf - 1));
            outline.Add(At(terminator * Math.Cos(angle), Math.Sin(angle)));
        }
        return outline;
    }
}
