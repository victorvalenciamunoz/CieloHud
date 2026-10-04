namespace CieloHud.Core.Constellations;

/// <summary>A J2000 direction on the sky, in degrees.</summary>
public readonly record struct EquatorialPoint(double RightAscensionDegrees, double DeclinationDegrees);

/// <summary>The usual stick figure of a constellation: a few polylines joining its brighter stars.</summary>
/// <param name="Symbol">Three-letter IAU abbreviation (e.g. "Ori").</param>
/// <param name="Rank">1 = major, 2 = medium, 3 = minor constellation (as ranked by the data source).</param>
/// <param name="Lines">Polylines; each is a sequence of J2000 vertices joined in order.</param>
public sealed record ConstellationFigure(string Symbol, int Rank, IReadOnlyList<IReadOnlyList<EquatorialPoint>> Lines);

/// <summary>
/// The stick figures of the 88 IAU constellations. Unlike the boundaries, figures are not official; these follow the
/// common modern drawing used by d3-celestial. Data in <c>ConstellationFigures.Data.cs</c>, generated from its JSON.
/// </summary>
public static partial class ConstellationFigures
{
    private static readonly Lazy<IReadOnlyDictionary<string, ConstellationFigure>> BySymbol =
        new(() => Build().ToDictionary(f => f.Symbol, StringComparer.Ordinal));

    public static IReadOnlyCollection<ConstellationFigure> All => (IReadOnlyCollection<ConstellationFigure>)BySymbol.Value.Values;

    /// <summary>Figure for an IAU symbol, or null when the symbol is unknown.</summary>
    public static ConstellationFigure? Get(string symbol) => BySymbol.Value.GetValueOrDefault(symbol);

    /// <summary>Builds a figure from flat polylines: [ra, dec, ra, dec, …] per line.</summary>
    private static ConstellationFigure F(string symbol, int rank, params double[][] lines) =>
        new(symbol, rank, lines
            .Select(flat => (IReadOnlyList<EquatorialPoint>)Enumerable.Range(0, flat.Length / 2)
                .Select(i => new EquatorialPoint(flat[2 * i], flat[2 * i + 1]))
                .ToList())
            .ToList());
}
