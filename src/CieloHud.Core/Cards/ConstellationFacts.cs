using CieloHud.Core.Constellations;
using CieloHud.Core.Sky;
using CieloHud.Core.Stars;

namespace CieloHud.Core.Cards;

/// <summary>How much of a constellation rises from the observer's latitude, at some time of the night and the year.</summary>
public enum ConstellationSight
{
    /// <summary>Every part of it rises above the horizon at some time.</summary>
    Whole,

    /// <summary>Some of it rises, the rest never does.</summary>
    Partly,

    /// <summary>None of it ever rises.</summary>
    NeverRises,

    /// <summary>All of it stays above the horizon all the time.</summary>
    NeverSets,
}

/// <summary>What a constellation's card says about it (decision 047).</summary>
/// <param name="Symbol">Three-letter IAU abbreviation.</param>
/// <param name="SkyFraction">Share of the whole sky inside its boundaries, 0 to 1.</param>
/// <param name="SizeRank">1 for the largest of the 88, 88 for the smallest.</param>
/// <param name="Sight">How much of it rises from the observer's latitude.</param>
/// <param name="BrightestStars">
/// Its stars brighter than magnitude 3, brightest first (empty if none is), or null when the catalog does not reach that far
/// south and cannot tell (<see cref="ConstellationStars.CatalogCovers"/>).
/// </param>
public sealed record ConstellationFacts(string Symbol, double SkyFraction, int SizeRank, ConstellationSight Sight, IReadOnlyList<Star>? BrightestStars)
{
    public static ConstellationFacts Of(string symbol, Observer observer)
    {
        var extent = ConstellationExtents.Get(symbol) ?? throw new ArgumentException($"Unknown constellation '{symbol}'.", nameof(symbol));
        return new ConstellationFacts(
            symbol,
            extent.AreaSquareDegrees / ConstellationExtents.SkySquareDegrees,
            ConstellationExtents.SizeRank(symbol),
            SightFrom(extent, observer.LatitudeDegrees),
            ConstellationStars.CatalogCovers(symbol) ? ConstellationStars.In(symbol) : null);
    }

    /// <summary>
    /// Geometry only, as in Ridpath's table of the constellations: a point at declination δ rises at some time if it culminates above the
    /// horizon (90° − |φ − δ| &gt; 0) and never sets if its lower culmination is above it too (|φ + δ| − 90° &gt; 0, on the side of the
    /// observer's pole). No refraction, no mountains, no haze: near the horizon a star is much dimmer.
    /// </summary>
    public static ConstellationSight SightFrom(ConstellationExtent extent, double latitudeDegrees)
    {
        ArgumentNullException.ThrowIfNull(extent);
        var (south, north) = (extent.MinDeclinationDegrees, extent.MaxDeclinationDegrees);
        var lat = latitudeDegrees;

        var neverSets = lat >= 0 ? south > 90 - lat : north < -90 - lat;
        if (neverSets)
            return ConstellationSight.NeverSets;
        if (south > lat - 90 && north < lat + 90)
            return ConstellationSight.Whole;
        if (north <= lat - 90 || south >= lat + 90)
            return ConstellationSight.NeverRises;
        return ConstellationSight.Partly;
    }
}
