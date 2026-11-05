using CieloHud.Core.Constellations;

namespace CieloHud.Core.Tests.Cards;

/// <summary>Whether a card key names an object of the app: a target, a star of the catalog or an IAU constellation.</summary>
internal static class KnownCards
{
    public static bool Exists(CardKey key) => key.Kind switch
    {
        CardKind.Target => key.Id == CardKey.IssId || Enum.TryParse<CelestialBody>(key.Id, out _),
        CardKind.Star => BrightStars.All.Any(s => s.Designation == key.Id),
        CardKind.Constellation => ConstellationFigures.Get(key.Id) is not null,
        _ => false,
    };
}
