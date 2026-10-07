namespace CieloHud.Core.Cards;

/// <summary>The four moons Galileo saw in 1610, inner to outer.</summary>
public enum GalileanMoonName
{
    Io,
    Europa,
    Ganymede,
    Callisto,
}

/// <summary>Whether a moon can be told apart from Jupiter (decision 039).</summary>
public enum GalileanMoonState
{
    Visible,

    /// <summary>Its center is behind Jupiter's disc: occulted.</summary>
    BehindJupiter,

    /// <summary>Its center is in front of Jupiter's disc: a transit, lost in the planet's glare.</summary>
    InFrontOfJupiter,

    /// <summary>Its center is in Jupiter's shadow: eclipsed, dark.</summary>
    InJupitersShadow,
}

/// <summary>
/// One moon as the observer sees it next to Jupiter, in Jupiter equatorial radii on the plane of the sky:
/// <paramref name="RightRadii"/> towards increasing azimuth (right when facing Jupiter with the phone upright),
/// <paramref name="UpRadii"/> towards the zenith.
/// </summary>
/// <param name="DepthRadii">Along the line of sight: positive behind Jupiter, negative in front.</param>
public sealed record GalileanMoon(GalileanMoonName Name, double RightRadii, double UpRadii, double DepthRadii, GalileanMoonState State);

/// <summary>Jupiter's four big moons right now, for its card.</summary>
/// <param name="JupiterRadiusArcseconds">Apparent equatorial radius of Jupiter: the scale of the offsets.</param>
public sealed record JupiterMoonsFacts(IReadOnlyList<GalileanMoon> Moons, double JupiterRadiusArcseconds);
