using CieloHud.Core.Sky;
using CieloHud.Core.Stars;

namespace CieloHud.Core.Constellations;

/// <summary><see cref="IConstellationFigureLocator"/> using the same J2000 → horizon path as the stars.</summary>
public sealed class AstronomyEngineConstellationFigureLocator : IConstellationFigureLocator
{
    public IReadOnlyList<IReadOnlyList<HorizontalPosition>> Locate(string symbol, Observer observer, DateTimeOffset instant)
    {
        if (ConstellationFigures.Get(symbol) is not { } figure)
            return [];

        // One batch per figure so the precession matrix is computed once.
        var flat = J2000Sky.LocateAll(
            figure.Lines.SelectMany(line => line.Select(p => (p.RightAscensionDegrees, p.DeclinationDegrees))),
            observer, instant);

        var result = new List<IReadOnlyList<HorizontalPosition>>(figure.Lines.Count);
        var index = 0;
        foreach (var line in figure.Lines)
        {
            result.Add(flat.Skip(index).Take(line.Count).ToList());
            index += line.Count;
        }
        return result;
    }
}
