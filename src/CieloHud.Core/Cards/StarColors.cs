using CieloHud.Core.Stars;

namespace CieloHud.Core.Cards;

/// <summary>The color a star's light shows, in the words of popular astronomy (decision 044).</summary>
public enum StarColor
{
    Bluish,
    White,
    YellowishWhite,
    Yellowish,
    Orange,
    Reddish,
}

/// <summary>
/// The color from the class letter of the spectral type, as popular astronomy teaches it: O and B bluish, A white, F yellowish
/// white, G yellowish, K orange, M reddish. Not from the B−V index: its usual limits are set on dwarf stars and, on these giants,
/// called 42 of the 155 by the next color (Aldebaran "reddish", Vega "bluish" by a thousandth).
/// </summary>
public static class StarColors
{
    private const string Classes = "WOBAFGKM";

    public static StarColor Of(Star star)
    {
        ArgumentNullException.ThrowIfNull(star);
        return Of(star.SpectralType);
    }

    /// <summary>
    /// The first class letter in the type: peculiar prefixes (the "k" of "kA4hA5mA5Va") and lower-case letters are skipped.
    /// Wolf-Rayet stars (W) are hot and blue like O.
    /// </summary>
    public static StarColor Of(string spectralType)
    {
        ArgumentNullException.ThrowIfNull(spectralType);
        var letter = spectralType.FirstOrDefault(c => Classes.Contains(c));
        return letter switch
        {
            'W' or 'O' or 'B' => StarColor.Bluish,
            'A' => StarColor.White,
            'F' => StarColor.YellowishWhite,
            'G' => StarColor.Yellowish,
            'K' => StarColor.Orange,
            'M' => StarColor.Reddish,
            _ => throw new FormatException($"No spectral class in '{spectralType}'."),
        };
    }
}
