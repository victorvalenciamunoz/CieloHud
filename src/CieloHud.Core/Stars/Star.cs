namespace CieloHud.Core.Stars;

/// <summary>
/// A fixed star from the catalog. Coordinates are ICRS/J2000 as published by SIMBAD; proper motion is ignored
/// (under 0.05° since 2000 for the stars in <see cref="BrightStars"/>).
/// </summary>
/// <param name="ProperName">IAU proper name (e.g. "Sirius"), or null when the IAU has not named the star.</param>
/// <param name="Designation">Bayer designation as used by SIMBAD (e.g. "alf CMa", "gam Cas", "tet01 Eri").</param>
/// <param name="RightAscensionDegrees">J2000 right ascension, degrees.</param>
/// <param name="DeclinationDegrees">J2000 declination, degrees.</param>
/// <param name="Magnitude">Visual magnitude (Johnson V); lower is brighter.</param>
/// <param name="Hipparcos">Number in the Hipparcos catalog, where its parallax comes from.</param>
/// <param name="ParallaxMas">Parallax in milliarcseconds (Hipparcos, new reduction): the distance in parsecs is 1000 over it.</param>
/// <param name="ParallaxErrorMas">Standard error of the parallax, milliarcseconds.</param>
/// <param name="SpectralType">MK spectral type as SIMBAD gives it ("M1-M2Ia-Iab"); its class letter sets the color on the card.</param>
public sealed record Star(
    string? ProperName, string Designation, double RightAscensionDegrees, double DeclinationDegrees, double Magnitude,
    int Hipparcos, double ParallaxMas, double ParallaxErrorMas, string SpectralType)
{
    /// <summary>Proper name when there is one, otherwise the Bayer designation. Display names belong to the UI.</summary>
    public string Name => ProperName ?? Designation;
}
