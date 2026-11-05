namespace CieloHud.Core.Cards;

/// <summary>
/// When a Galilean moon cannot be told apart from Jupiter (decision 039). Judged by the moon's center against Jupiter's
/// equatorial radius: the disc's 6.5 % flattening and the moons' own size (under 0.04 Jupiter radii) shift an event by a
/// minute or two, and the moons cross near Jupiter's equator anyway.
/// </summary>
public static class GalileanMoons
{
    /// <summary>
    /// Occulted wins over eclipsed (it is hidden either way), and eclipsed over a transit (dark against the disc).
    /// </summary>
    /// <param name="rightRadii">Offset on the sky, in Jupiter radii.</param>
    /// <param name="upRadii">Offset on the sky, in Jupiter radii.</param>
    /// <param name="depthRadii">Positive when the moon is farther than Jupiter.</param>
    public static GalileanMoonState State(double rightRadii, double upRadii, double depthRadii, bool inShadow)
    {
        var onDisc = rightRadii * rightRadii + upRadii * upRadii < 1;
        if (onDisc && depthRadii > 0)
            return GalileanMoonState.BehindJupiter;
        if (inShadow)
            return GalileanMoonState.InJupitersShadow;
        return onDisc ? GalileanMoonState.InFrontOfJupiter : GalileanMoonState.Visible;
    }

    /// <summary>
    /// Jupiter's shadow as a cylinder of its radius pointing away from the Sun. The true umbra is a cone that narrows by
    /// under 3 % at Callisto's distance, and the penumbra a few seconds of the moon's motion: not worth a cone.
    /// </summary>
    /// <param name="alongRadii">Distance from Jupiter's center along the Sun-to-Jupiter direction, in Jupiter radii.</param>
    /// <param name="perpendicularRadii">Distance from that axis, in Jupiter radii.</param>
    public static bool InShadow(double alongRadii, double perpendicularRadii) => alongRadii > 0 && perpendicularRadii < 1;
}
