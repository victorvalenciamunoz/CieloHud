namespace CieloHud.Core.Stars;

/// <summary>
/// A fixed star from the catalog. Coordinates are ICRS/J2000 as published by SIMBAD; proper motion is ignored
/// (under 0.05° since 2000 for every star in <see cref="BrightStars"/>).
/// </summary>
/// <param name="Name">IAU proper name (e.g. "Sirius"). Display names in other languages belong to the UI.</param>
/// <param name="Designation">Bayer designation as used by SIMBAD (e.g. "alf CMa").</param>
/// <param name="RightAscensionDegrees">J2000 right ascension, degrees.</param>
/// <param name="DeclinationDegrees">J2000 declination, degrees.</param>
/// <param name="Magnitude">Visual magnitude (Johnson V); lower is brighter.</param>
public sealed record Star(string Name, string Designation, double RightAscensionDegrees, double DeclinationDegrees, double Magnitude);
