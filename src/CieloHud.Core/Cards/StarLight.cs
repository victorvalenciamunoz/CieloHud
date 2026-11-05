using CieloHud.Core.Stars;

namespace CieloHud.Core.Cards;

/// <summary>How sure the distance is, from the parallax's relative error (decision 044).</summary>
public enum DistanceCertainty
{
    /// <summary>Error up to 5 %: the figure as it is.</summary>
    Precise,

    /// <summary>Up to 20 %: "about".</summary>
    About,

    /// <summary>Up to 50 %: the range one standard error either side.</summary>
    Between,

    /// <summary>Beyond 50 %, or a parallax no larger than its error: only the near end means anything.</summary>
    MoreThan,
}

/// <summary>
/// How long ago the light now arriving left a star, in years: its distance in light years.
/// <paramref name="NearYears"/> and <paramref name="FarYears"/> are the distances one standard error of the parallax either side
/// (<see cref="double.PositiveInfinity"/> when the parallax is no larger than its error).
/// </summary>
public readonly record struct StarLight(double Years, double NearYears, double FarYears, DistanceCertainty Certainty)
{
    /// <summary>Light years in a parsec (IAU: 1 pc = 648 000/π au; 1 light year = 63 241.077 au).</summary>
    public const double LightYearsPerParsec = 3.261_563_777;

    public static StarLight Of(Star star)
    {
        ArgumentNullException.ThrowIfNull(star);
        return Of(star.ParallaxMas, star.ParallaxErrorMas);
    }

    public static StarLight Of(double parallaxMas, double parallaxErrorMas)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parallaxMas);
        ArgumentOutOfRangeException.ThrowIfNegative(parallaxErrorMas);

        double Years(double mas) => 1000 / mas * LightYearsPerParsec;
        var far = parallaxMas > parallaxErrorMas ? Years(parallaxMas - parallaxErrorMas) : double.PositiveInfinity;
        var relative = parallaxErrorMas / parallaxMas;
        var certainty = relative switch
        {
            <= 0.05 => DistanceCertainty.Precise,
            <= 0.20 => DistanceCertainty.About,
            <= 0.50 => DistanceCertainty.Between,
            _ => DistanceCertainty.MoreThan,
        };
        return new StarLight(Years(parallaxMas), Years(parallaxMas + parallaxErrorMas), far, certainty);
    }
}
