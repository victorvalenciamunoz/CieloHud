using CieloHud.Core.Sky;

namespace CieloHud.Core.Stars;

/// <summary>Locates fixed stars in the observer's sky.</summary>
public interface IStarService
{
    /// <summary>Apparent (refracted) horizontal position. Distance is not reported.</summary>
    HorizontalPosition Locate(Star star, Observer observer, DateTimeOffset instant);
}
