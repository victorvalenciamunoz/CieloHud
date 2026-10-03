using CieloHud.Core.Sky;

namespace CieloHud.Core.Constellations;

/// <summary>Tells which constellation a direction in the observer's sky falls in.</summary>
public interface IConstellationLocator
{
    /// <param name="azimuthDegrees">Apparent azimuth, 0° = north, clockwise.</param>
    /// <param name="altitudeDegrees">Apparent (refracted) altitude.</param>
    Constellation Locate(double azimuthDegrees, double altitudeDegrees, Observer observer, DateTimeOffset instant);
}
