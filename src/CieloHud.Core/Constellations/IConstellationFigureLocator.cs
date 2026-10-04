using CieloHud.Core.Sky;

namespace CieloHud.Core.Constellations;

/// <summary>Places a constellation's stick figure in the observer's sky.</summary>
public interface IConstellationFigureLocator
{
    /// <summary>
    /// The figure's polylines as apparent horizontal positions, or an empty list for an unknown symbol.
    /// Vertices below the horizon are included; drawing code decides what to do with them.
    /// </summary>
    IReadOnlyList<IReadOnlyList<HorizontalPosition>> Locate(string symbol, Observer observer, DateTimeOffset instant);
}
