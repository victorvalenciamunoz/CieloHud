namespace CieloHud.Core.Constellations;

/// <summary>One of the 88 IAU constellations.</summary>
/// <param name="Symbol">Three-letter IAU abbreviation (e.g. "Ori").</param>
/// <param name="Name">Latin name (e.g. "Orion"). Display names in other languages belong to the UI.</param>
public sealed record Constellation(string Symbol, string Name);
