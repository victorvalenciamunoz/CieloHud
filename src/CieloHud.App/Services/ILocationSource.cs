using CieloHud.Core.Sky;

namespace CieloHud.App.Services;

public sealed record LocationFix(Observer Observer, double? AccuracyMeters, DateTimeOffset Timestamp);

/// <summary>Gets the observer's position from the device. Asks for permission when needed.</summary>
public interface ILocationSource
{
    /// <summary>Last known or fresh fix; null when permission is denied or no fix is available.</summary>
    Task<LocationFix?> GetAsync(CancellationToken cancellationToken = default);
}
