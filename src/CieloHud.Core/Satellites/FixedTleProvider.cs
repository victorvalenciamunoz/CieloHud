namespace CieloHud.Core.Satellites;

/// <summary>
/// Serves TLEs handed in at construction. For tests and for running with elements supplied by hand.
/// </summary>
public sealed class FixedTleProvider : ITleProvider
{
    private readonly Dictionary<int, Tle> _tles;

    public FixedTleProvider(params IEnumerable<Tle> tles)
    {
        _tles = tles.ToDictionary(t => t.NoradNumber);
    }

    public Task<Tle> GetTleAsync(int noradNumber, CancellationToken cancellationToken = default)
    {
        return _tles.TryGetValue(noradNumber, out var tle)
            ? Task.FromResult(tle)
            : throw new KeyNotFoundException($"No TLE configured for NORAD {noradNumber}.");
    }
}
