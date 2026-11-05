namespace CieloHud.Core.Satellites;

/// <summary>Tells whether a satellite is lit by the Sun, which is what makes it visible against a dark sky.</summary>
public interface ISatelliteIlluminationService
{
    bool IsSunlit(Tle tle, DateTimeOffset instant);
}
