using CieloHud.Core.Sky;

namespace CieloHud.Core.Guidance;

/// <summary>Something in the sky that could be identified, with where it is now.</summary>
/// <param name="Magnitude">Brightness for stars (lower = brighter); null for objects that always win ties (Moon, planets, ISS).</param>
public sealed record SkyCandidate(string Name, HorizontalPosition Position, double? Magnitude = null);

/// <summary>The candidate chosen for where the device points, and its true angular distance.</summary>
public readonly record struct Identification(SkyCandidate Candidate, double AngularDistanceDegrees);

/// <summary>
/// Answers "what am I pointing at?": the same geometry as guidance, run over every candidate.
/// Phone compasses are good to a few degrees, so when several objects are in range the brighter one is preferred:
/// each magnitude above 0 counts as <see cref="DegreesPerMagnitude"/> of extra distance. What you see first is what you
/// are most likely pointing at. Objects below the horizon are ignored by default.
/// </summary>
public static class SkyIdentifier
{
    /// <summary>A little wider than the on-target zone.</summary>
    public const double DefaultRadiusDegrees = 5;

    /// <summary>Penalty per magnitude, in degrees: a magnitude-3 star must be 3° closer than a magnitude-0 one to win.</summary>
    public const double DegreesPerMagnitude = 1.0;

    /// <summary>Best candidate within <paramref name="radiusDegrees"/>, or null when nothing is close enough.</summary>
    public static Identification? Identify(
        PointingDirection pointing,
        IEnumerable<SkyCandidate> candidates,
        double radiusDegrees = DefaultRadiusDegrees,
        double minAltitudeDegrees = 0)
    {
        if (radiusDegrees <= 0)
            throw new ArgumentOutOfRangeException(nameof(radiusDegrees), radiusDegrees, "Radius must be positive.");

        return Best(pointing, candidates, minAltitudeDegrees, radiusDegrees);
    }

    /// <summary>Best visible candidate at any distance (same brightness preference), or null when none is up.</summary>
    public static Identification? Nearest(PointingDirection pointing, IEnumerable<SkyCandidate> candidates, double minAltitudeDegrees = 0) =>
        Best(pointing, candidates, minAltitudeDegrees, double.PositiveInfinity);

    /// <summary>Distance plus brightness penalty; lower is better.</summary>
    public static double Score(double angularDistanceDegrees, double? magnitude) =>
        angularDistanceDegrees + DegreesPerMagnitude * Math.Max(0, magnitude ?? 0);

    private static Identification? Best(PointingDirection pointing, IEnumerable<SkyCandidate> candidates, double minAltitudeDegrees, double radiusDegrees)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        Identification? best = null;
        var bestScore = double.PositiveInfinity;
        foreach (var candidate in candidates)
        {
            if (candidate.Position.AltitudeDegrees < minAltitudeDegrees)
                continue;
            var distance = GuidanceCalculator.AngularDistance(
                pointing.AzimuthDegrees, pointing.AltitudeDegrees,
                candidate.Position.AzimuthDegrees, candidate.Position.AltitudeDegrees);
            if (distance > radiusDegrees)
                continue;
            var score = Score(distance, candidate.Magnitude);
            if (score < bestScore)
            {
                bestScore = score;
                best = new Identification(candidate, distance);
            }
        }
        return best;
    }
}
