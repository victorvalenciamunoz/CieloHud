namespace CieloHud.Core.Apparitions;

/// <summary>
/// When Mercury is worth going out for (decision 033): high enough to clear buildings and haze, in a dark enough sky (the same
/// limits as ISS passes and conjunctions), and bright enough to pick out low in the twilight. Fixed by design.
/// </summary>
public sealed record MercuryCriteria
{
    /// <summary>Mercury must be at least this high.</summary>
    public double MinAltitudeDegrees { get; init; } = 10;

    /// <summary>The Sun must be below this geometric altitude (civil twilight), as for passes and conjunctions.</summary>
    public double MaxSunAltitudeDegrees { get; init; } = -6;

    /// <summary>
    /// Mercury must be at least this bright (magnitude at most this). Low in the twilight, through ten times more air than overhead,
    /// a fainter Mercury is hard to find from a city. It trims the end of the spring seasons, when Mercury fades fast.
    /// </summary>
    public double MaxMagnitude { get; init; } = 0.5;

    /// <summary>Sampling step of the windows: they last 2 to 21 minutes, so 5 minutes would miss days at the edges of a season.</summary>
    public TimeSpan Step { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Step of the first, coarse pass over the whole range. Only where it finds Mercury and the Sun near the limits is the sky
    /// sampled every <see cref="Step"/>.
    /// </summary>
    public TimeSpan CoarseStep { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How near the limits the coarse pass must be to sample finely. Mercury and the Sun move at most 1.25° in 5 minutes,
    /// so a fine sample meeting the limits always has a coarse neighbor within this margin.
    /// </summary>
    public double CoarseMarginDegrees { get; init; } = 2;

    /// <summary>
    /// A season lasts up to about three weeks. To tell which is its best day, the search looks this far before and after the
    /// requested range.
    /// </summary>
    public TimeSpan ApparitionSearch { get; init; } = TimeSpan.FromDays(30);

    public static MercuryCriteria Default { get; } = new();

    internal void Validate()
    {
        if (Step <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(Step), Step, "Step must be positive.");
        if (CoarseStep < Step || CoarseStep.Ticks % Step.Ticks != 0)
            throw new ArgumentException("CoarseStep must be a whole multiple of Step.", nameof(CoarseStep));
        if (CoarseMarginDegrees < 0)
            throw new ArgumentOutOfRangeException(nameof(CoarseMarginDegrees), CoarseMarginDegrees, "Cannot be negative.");
        if (ApparitionSearch < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ApparitionSearch), ApparitionSearch, "Cannot be negative.");
    }
}
