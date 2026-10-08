namespace CieloHud.Core.Constellations;

/// <summary>How much sky a constellation covers and how far north and south it reaches, within its official boundaries.</summary>
/// <param name="Symbol">Three-letter IAU abbreviation (e.g. "Ori").</param>
/// <param name="AreaSquareDegrees">Area in square degrees; the 88 add up to <see cref="ConstellationExtents.SkySquareDegrees"/>.</param>
/// <param name="MinDeclinationDegrees">Southernmost J2000 declination of its boundaries (−90 if it holds the south pole).</param>
/// <param name="MaxDeclinationDegrees">Northernmost J2000 declination of its boundaries (90 if it holds the north pole).</param>
public sealed record ConstellationExtent(string Symbol, double AreaSquareDegrees, double MinDeclinationDegrees, double MaxDeclinationDegrees);

/// <summary>
/// Area and declination range of the 88 IAU constellations (decision 047). Data in <c>ConstellationExtents.Data.cs</c>,
/// generated from the official boundaries.
/// </summary>
public static partial class ConstellationExtents
{
    /// <summary>The whole sky: 4π steradians, 129 600/π ≈ 41 253 square degrees.</summary>
    public const double SkySquareDegrees = 129_600 / Math.PI;

    private static readonly Lazy<IReadOnlyDictionary<string, ConstellationExtent>> BySymbol =
        new(() => Build().ToDictionary(e => e.Symbol, StringComparer.Ordinal));

    private static readonly Lazy<IReadOnlyDictionary<string, int>> Ranks =
        new(() => BySymbol.Value.Values
            .OrderByDescending(e => e.AreaSquareDegrees)
            .Select((e, i) => (e.Symbol, Rank: i + 1))
            .ToDictionary(x => x.Symbol, x => x.Rank, StringComparer.Ordinal));

    public static IReadOnlyCollection<ConstellationExtent> All => (IReadOnlyCollection<ConstellationExtent>)BySymbol.Value.Values;

    /// <summary>Extent for an IAU symbol, or null when the symbol is unknown.</summary>
    public static ConstellationExtent? Get(string symbol) => BySymbol.Value.GetValueOrDefault(symbol);

    /// <summary>Place by area: 1 is the largest (the Hydra), 88 the smallest (the Southern Cross).</summary>
    public static int SizeRank(string symbol) =>
        Ranks.Value.TryGetValue(symbol, out var rank) ? rank : throw new ArgumentException($"Unknown constellation '{symbol}'.", nameof(symbol));
}
