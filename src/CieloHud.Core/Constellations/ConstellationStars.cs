using CieloHud.Core.Stars;
using CosineKitty;

namespace CieloHud.Core.Constellations;

/// <summary>
/// The stars of the catalog (<see cref="BrightStars"/>) inside each constellation's official boundaries, looked up with
/// Astronomy Engine's <c>Constellation</c> (Roman 1987) at their J2000 position.
/// </summary>
public static class ConstellationStars
{
    private static readonly Lazy<ILookup<string, Star>> BySymbol =
        new(() => BrightStars.All.OrderBy(s => s.Magnitude).ToLookup(s => SymbolAt(s.RightAscensionDegrees, s.DeclinationDegrees)));

    /// <summary>The catalog's stars in the constellation, brightest first; empty if none is that bright.</summary>
    public static IReadOnlyList<Star> In(string symbol) => [.. BySymbol.Value[symbol]];

    /// <summary>
    /// Whether the catalog holds every star of the constellation brighter than magnitude 3: only when the whole constellation is
    /// north of <see cref="BrightStars.SouthernLimitDegrees"/>. Centaurus has five stars in it, but not Alpha Centauri.
    /// </summary>
    public static bool CatalogCovers(string symbol) =>
        (ConstellationExtents.Get(symbol) ?? throw new ArgumentException($"Unknown constellation '{symbol}'.", nameof(symbol)))
            .MinDeclinationDegrees > BrightStars.SouthernLimitDegrees;

    /// <summary>IAU symbol of the constellation holding a J2000 direction.</summary>
    public static string SymbolAt(double rightAscensionDegrees, double declinationDegrees) =>
        Astronomy.Constellation(rightAscensionDegrees / 15, declinationDegrees).Symbol;
}
