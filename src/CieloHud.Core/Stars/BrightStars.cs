namespace CieloHud.Core.Stars;

/// <summary>
/// The stars worth naming from a city: every star brighter than magnitude 1.65 that rises above 40° north,
/// plus Polaris for orientation. Data from the SIMBAD TAP service (CDS, Strasbourg), queried on 2026-10-03:
/// <c>SELECT main_id, ra, dec, V FROM basic JOIN allfluxes WHERE V &lt; 1.65 AND dec &gt; -50</c>.
/// </summary>
public static class BrightStars
{
    public static IReadOnlyList<Star> All { get; } =
    [
        new("Sirius", "alf CMa", 101.28715533333335, -16.71611586111111, -1.46),
        new("Arcturus", "alf Boo", 213.915300294925, 19.1824091615312, -0.05),
        new("Vega", "alf Lyr", 279.234734787025, 38.783688956244, 0.03),
        new("Capella", "alf Aur", 79.17232794433404, 45.99799146983673, 0.08),
        new("Rigel", "bet Ori", 78.63446706693006, -8.201638364722209, 0.13),
        new("Procyon", "alf CMi", 114.82549790798149, 5.224987557059477, 0.37),
        new("Betelgeuse", "alf Ori", 88.79293899077537, 7.407063995272694, 0.42),
        new("Altair", "alf Aql", 297.69582729638694, 8.868321196436963, 0.76),
        new("Aldebaran", "alf Tau", 68.9801627900154, 16.5093023507718, 0.86),
        new("Antares", "alf Sco", 247.3519154198264, -26.432002611950832, 0.91),
        new("Spica", "alf Vir", 201.2982473615632, -11.161319485111932, 0.97),
        new("Pollux", "bet Gem", 116.32895777437875, 28.02619889009357, 1.14),
        new("Fomalhaut", "alf PsA", 344.4126927211701, -29.622237033389442, 1.16),
        new("Deneb", "alf Cyg", 310.35797975307673, 45.280338806527574, 1.25),
        new("Regulus", "alf Leo", 152.09296243828146, 11.967208776100023, 1.40),
        new("Adhara", "eps CMa", 104.65645315148348, -28.972086157360806, 1.50),
        new("Castor", "alf Gem", 113.64947163976585, 31.88828221646326, 1.58),
        new("Shaula", "lam Sco", 263.40216718438023, -37.10382355111976, 1.63),
        new("Bellatrix", "gam Ori", 81.28276355652378, 6.3497032644440665, 1.64),
        new("Elnath", "bet Tau", 81.57297133176498, 28.607451724998228, 1.65),
        new("Polaris", "alf UMi", 37.954560670189856, 89.26410896994187, 2.02),
    ];

    public static Star Get(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"No star named '{name}' in the catalog.");
}
