using CieloHud.Core.Satellites;
using CieloHud.Core.Sky;

namespace CieloHud.Core.Passes;

/// <summary>Finds when a satellite is above the observer's horizon.</summary>
public interface ISatellitePassPredictor
{
    /// <summary>
    /// Passes whose start lies in [<paramref name="from"/>, <paramref name="to"/>), in chronological order.
    /// A pass already in progress at <paramref name="from"/> is reported starting at <paramref name="from"/>;
    /// a pass that begins before <paramref name="to"/> is reported in full even if it ends later.
    /// </summary>
    IReadOnlyList<SatellitePass> Predict(Tle tle, Observer observer, DateTimeOffset from, DateTimeOffset to);
}
