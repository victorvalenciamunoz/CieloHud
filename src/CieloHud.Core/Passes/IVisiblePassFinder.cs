using CieloHud.Core.Satellites;
using CieloHud.Core.Sky;

namespace CieloHud.Core.Passes;

/// <summary>Finds the passes an observer can actually see.</summary>
public interface IVisiblePassFinder
{
    /// <summary>Visible passes whose geometric pass starts in [<paramref name="from"/>, <paramref name="to"/>), in chronological order.</summary>
    IReadOnlyList<VisiblePass> Find(Tle tle, Observer observer, DateTimeOffset from, DateTimeOffset to);
}
