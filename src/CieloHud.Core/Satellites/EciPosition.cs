namespace CieloHud.Core.Satellites;

/// <summary>Position in Earth-centered inertial coordinates, kilometers.</summary>
public readonly record struct EciPosition(double XKm, double YKm, double ZKm)
{
    public double Length => Math.Sqrt(XKm * XKm + YKm * YKm + ZKm * ZKm);

    public double Dot(EciPosition other) => XKm * other.XKm + YKm * other.YKm + ZKm * other.ZKm;
}
