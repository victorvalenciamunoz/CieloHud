namespace CieloHud.Core.Cards;

/// <summary>
/// The last thing recognized, kept for <see cref="Grace"/> after it stops being recognized (decision 038). With a few degrees of
/// compass noise the reticle drifts in and out of an object; without this, "VER FICHA" would flicker under the user's finger.
/// A different object replaces it at once.
/// </summary>
public sealed class RecentMatch<T> where T : class
{
    public static readonly TimeSpan DefaultGrace = TimeSpan.FromSeconds(2);

    private T? _value;
    private DateTimeOffset _lastSeen;

    public RecentMatch(TimeSpan? grace = null)
    {
        Grace = grace ?? DefaultGrace;
        if (Grace < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(grace), grace, "Grace cannot be negative.");
    }

    public TimeSpan Grace { get; }

    /// <summary>Call every frame with what is recognized now (null for nothing); returns what to offer.</summary>
    public T? Update(T? current, DateTimeOffset now)
    {
        if (current is not null)
        {
            _value = current;
            _lastSeen = now;
            return current;
        }
        if (_value is not null && now - _lastSeen > Grace)
            _value = null;
        return _value;
    }

    public void Clear() => _value = null;
}
