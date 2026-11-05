namespace CieloHud.Core.Cards;

/// <summary>
/// The constellation under the reticle, steadied (decision 048). There is always one, so <see cref="RecentMatch{T}"/> never holds
/// it: with a few degrees of compass noise the reticle crosses a boundary back and forth, and the name, which opens its card when
/// tapped, would change under the finger. The one shown stays while it was seen within <see cref="Hold"/>; another takes over
/// only once it has not been seen for that long.
/// </summary>
public sealed class StickyMatch<T> where T : class
{
    public static readonly TimeSpan DefaultHold = TimeSpan.FromSeconds(1);

    private T? _shown;
    private DateTimeOffset _shownSeen;

    public StickyMatch(TimeSpan? hold = null)
    {
        Hold = hold ?? DefaultHold;
        if (Hold < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(hold), hold, "Hold cannot be negative.");
    }

    public TimeSpan Hold { get; }

    /// <summary>Call every frame with what is under the reticle now (null for nothing); returns what to show.</summary>
    public T? Update(T? current, DateTimeOffset now)
    {
        if (current is not null && current.Equals(_shown))
        {
            _shownSeen = now;
            return _shown;
        }
        if (_shown is null || now - _shownSeen > Hold)
        {
            _shown = current;
            _shownSeen = now;
        }
        return _shown;
    }

    public void Clear() => _shown = null;
}
