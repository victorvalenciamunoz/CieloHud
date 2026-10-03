using CieloHud.Core.Sky;

namespace CieloHud.Core.Guidance;

/// <summary>Something in the sky that could be identified, with where it is now.</summary>
public sealed record SkyCandidate(string Name, HorizontalPosition Position);

/// <summary>The candidate closest to where the device points, and how far it is.</summary>
public readonly record struct Identification(SkyCandidate Candidate, double AngularDistanceDegrees);

/// <summary>
/// Answers "what am I pointing at?": the same geometry as guidance, run over every candidate.
/// Objects below the horizon are ignored by default: you cannot see them, however well you point.
/// </summary>
public static class SkyIdentifier
{
    /// <summary>A little wider than the on-target zone: phone compasses are good to a few degrees.</summary>
    public const double DefaultRadiusDegrees = 5;

    /// <summary>Nearest candidate within <paramref name="radiusDegrees"/>, or null when nothing is close enough.</summary>
    public static Identification? Identify(
        PointingDirection pointing,
        IEnumerable<SkyCandidate> candidates,
        double radiusDegrees = DefaultRadiusDegrees,
        double minAltitudeDegrees = 0)
    {
        if (radiusDegrees <= 0)
            throw new ArgumentOutOfRangeException(nameof(radiusDegrees), radiusDegrees, "Radius must be positive.");

        return Nearest(pointing, candidates, minAltitudeDegrees) is { } nearest && nearest.AngularDistanceDegrees <= radiusDegrees
            ? nearest
            : null;
    }

    /// <summary>Nearest visible candidate at any distance, or null when none is above <paramref name="minAltitudeDegrees"/>.</summary>
    public static Identification? Nearest(PointingDirection pointing, IEnumerable<SkyCandidate> candidates, double minAltitudeDegrees = 0)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        Identification? best = null;
        foreach (var candidate in candidates)
        {
            if (candidate.Position.AltitudeDegrees < minAltitudeDegrees)
                continue;
            var distance = GuidanceCalculator.AngularDistance(
                pointing.AzimuthDegrees, pointing.AltitudeDegrees,
                candidate.Position.AzimuthDegrees, candidate.Position.AltitudeDegrees);
            if (best is null || distance < best.Value.AngularDistanceDegrees)
                best = new Identification(candidate, distance);
        }
        return best;
    }
}
