namespace CieloHud.Core.Cards;

/// <summary>
/// When to open a target's card by itself (decision 038): once the reticle has stayed on target for <see cref="Hold"/>,
/// and only once until <see cref="Reset"/> (a new target). Long enough to have lowered the phone and looked, short enough
/// to be there when you look back at it. Leaving the target before then starts the count again.
/// </summary>
public sealed class CardAutoOpen
{
    public static readonly TimeSpan DefaultHold = TimeSpan.FromSeconds(1.5);

    private DateTimeOffset? _onTargetSince;
    private bool _opened;

    public CardAutoOpen(TimeSpan? hold = null)
    {
        Hold = hold ?? DefaultHold;
        if (Hold < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(hold), hold, "Hold cannot be negative.");
    }

    public TimeSpan Hold { get; }

    /// <summary>Call every frame; true exactly once, on the frame the card should open.</summary>
    public bool Update(bool onTarget, DateTimeOffset now)
    {
        if (_opened)
            return false;
        if (!onTarget)
        {
            _onTargetSince = null;
            return false;
        }

        _onTargetSince ??= now;
        if (now - _onTargetSince.Value < Hold)
            return false;
        _opened = true;
        return true;
    }

    /// <summary>A new target: its card may open again.</summary>
    public void Reset()
    {
        _onTargetSince = null;
        _opened = false;
    }
}
