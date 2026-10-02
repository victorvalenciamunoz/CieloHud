namespace CieloHud.Core.Satellites;

/// <summary>
/// Supplies orbital elements for a satellite. Implementations decide where they come from (network, cache, fixed data).
/// </summary>
public interface ITleProvider
{
    /// <summary>
    /// Returns the most recent TLE available for the satellite with the given NORAD catalog number (ISS = 25544).
    /// Check <see cref="Tle.Epoch"/> to judge how old the elements are.
    /// </summary>
    Task<Tle> GetTleAsync(int noradNumber, CancellationToken cancellationToken = default);
}
