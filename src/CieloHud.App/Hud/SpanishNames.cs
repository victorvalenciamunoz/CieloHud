namespace CieloHud.App.Hud;

/// <summary>Spanish display names. Core keeps IAU/Latin names; translation is a UI concern.</summary>
public static class SpanishNames
{
    /// <summary>IAU proper names of the bright stars that have a usual Spanish form.</summary>
    public static readonly IReadOnlyDictionary<string, string> Stars = new Dictionary<string, string>
    {
        ["Sirius"] = "Sirio", ["Arcturus"] = "Arturo", ["Procyon"] = "Proción", ["Aldebaran"] = "Aldebarán",
        ["Spica"] = "Espiga", ["Pollux"] = "Pólux", ["Regulus"] = "Régulo", ["Castor"] = "Cástor",
        ["Polaris"] = "Estrella Polar",
    };

    /// <summary>
    /// The 88 IAU constellations by symbol, in the form used after "en" / "hacia": with the article when Spanish
    /// uses one ("la Ballena", "el Cisne") and without it for mythological and zodiac names ("Orión", "Piscis").
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Constellations = new Dictionary<string, string>
    {
        ["And"] = "Andrómeda", ["Ant"] = "la Máquina Neumática", ["Aps"] = "el Ave del Paraíso", ["Aqr"] = "Acuario",
        ["Aql"] = "el Águila", ["Ara"] = "el Altar", ["Ari"] = "Aries", ["Aur"] = "Auriga",
        ["Boo"] = "el Boyero", ["Cae"] = "el Cincel", ["Cam"] = "la Jirafa", ["Cnc"] = "Cáncer",
        ["CVn"] = "los Perros de Caza", ["CMa"] = "el Can Mayor", ["CMi"] = "el Can Menor", ["Cap"] = "Capricornio",
        ["Car"] = "la Quilla", ["Cas"] = "Casiopea", ["Cen"] = "Centauro", ["Cep"] = "Cefeo",
        ["Cet"] = "la Ballena", ["Cha"] = "el Camaleón", ["Cir"] = "el Compás", ["Col"] = "la Paloma",
        ["Com"] = "la Cabellera de Berenice", ["CrA"] = "la Corona Austral", ["CrB"] = "la Corona Boreal", ["Crv"] = "el Cuervo",
        ["Crt"] = "la Copa", ["Cru"] = "la Cruz del Sur", ["Cyg"] = "el Cisne", ["Del"] = "el Delfín",
        ["Dor"] = "el Dorado", ["Dra"] = "el Dragón", ["Equ"] = "el Caballito", ["Eri"] = "Erídano",
        ["For"] = "el Horno", ["Gem"] = "Géminis", ["Gru"] = "la Grulla", ["Her"] = "Hércules",
        ["Hor"] = "el Reloj", ["Hya"] = "la Hidra", ["Hyi"] = "la Hidra Macho", ["Ind"] = "el Indio",
        ["Lac"] = "el Lagarto", ["Leo"] = "Leo", ["LMi"] = "Leo Menor", ["Lep"] = "la Liebre",
        ["Lib"] = "Libra", ["Lup"] = "el Lobo", ["Lyn"] = "el Lince", ["Lyr"] = "la Lira",
        ["Men"] = "la Mesa", ["Mic"] = "el Microscopio", ["Mon"] = "el Unicornio", ["Mus"] = "la Mosca",
        ["Nor"] = "la Escuadra", ["Oct"] = "el Octante", ["Oph"] = "Ofiuco", ["Ori"] = "Orión",
        ["Pav"] = "el Pavo", ["Peg"] = "Pegaso", ["Per"] = "Perseo", ["Phe"] = "el Fénix",
        ["Pic"] = "el Pintor", ["Psc"] = "Piscis", ["PsA"] = "el Pez Austral", ["Pup"] = "la Popa",
        ["Pyx"] = "la Brújula", ["Ret"] = "el Retículo", ["Sge"] = "la Flecha", ["Sgr"] = "Sagitario",
        ["Sco"] = "Escorpio", ["Scl"] = "el Escultor", ["Sct"] = "el Escudo", ["Ser"] = "la Serpiente",
        ["Sex"] = "el Sextante", ["Tau"] = "Tauro", ["Tel"] = "el Telescopio", ["Tri"] = "el Triángulo",
        ["TrA"] = "el Triángulo Austral", ["Tuc"] = "el Tucán", ["UMa"] = "la Osa Mayor", ["UMi"] = "la Osa Menor",
        ["Vel"] = "la Vela", ["Vir"] = "Virgo", ["Vol"] = "el Pez Volador", ["Vul"] = "la Zorra",
    };

    public static string Star(string iauName) => Stars.GetValueOrDefault(iauName, iauName);

    /// <summary>Spanish name of the Greek letter abbreviations SIMBAD uses in Bayer designations.</summary>
    private static readonly IReadOnlyDictionary<string, string> GreekLetters = new Dictionary<string, string>
    {
        ["alf"] = "Alfa", ["bet"] = "Beta", ["gam"] = "Gamma", ["del"] = "Delta", ["eps"] = "Épsilon", ["zet"] = "Zeta",
        ["eta"] = "Eta", ["tet"] = "Theta", ["iot"] = "Iota", ["kap"] = "Kappa", ["lam"] = "Lambda", ["mu."] = "Mu",
        ["nu."] = "Nu", ["ksi"] = "Xi", ["omi"] = "Ómicron", ["pi."] = "Pi", ["rho"] = "Rho", ["sig"] = "Sigma",
        ["tau"] = "Tau", ["ups"] = "Ípsilon", ["phi"] = "Fi", ["chi"] = "Ji", ["psi"] = "Psi", ["ome"] = "Omega",
    };

    /// <summary>
    /// Display name for a catalog star: its proper name (in Spanish when there is a usual form), or for stars the IAU
    /// has not named, the Bayer designation in words: "gam Cas" → "Gamma de Casiopea", "gam02 Vel" → "Gamma 2 de la Vela",
    /// "eps Cyg" → "Épsilon del Cisne".
    /// </summary>
    public static string Star(Core.Stars.Star star)
    {
        if (star.ProperName is { } proper)
            return Star(proper);

        var parts = star.Designation.Split(' ', 2);
        if (parts.Length != 2)
            return star.Designation;

        var letterCode = parts[0].TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        var index = parts[0][letterCode.Length..].TrimStart('0');
        var letter = GreekLetters.GetValueOrDefault(letterCode, parts[0]);
        var constellation = Constellations.TryGetValue(parts[1], out var c) ? c : parts[1];
        var of = constellation.StartsWith("el ", StringComparison.Ordinal) ? "del " + constellation[3..] : "de " + constellation;
        return index.Length > 0 ? $"{letter} {index} {of}" : $"{letter} {of}";
    }

    /// <summary>"la Ballena", "Orión"…; falls back to the Latin name for an unknown symbol.</summary>
    public static string Constellation(Core.Constellations.Constellation c) => Constellations.GetValueOrDefault(c.Symbol, c.Name);

    /// <summary>"la Osa Mayor" → "Osa Mayor", "los Perros de Caza" → "Perros de Caza"; names without article unchanged.</summary>
    public static string WithoutArticle(string name)
    {
        foreach (var article in new[] { "el ", "la ", "los " })
            if (name.StartsWith(article, StringComparison.Ordinal))
                return name[article.Length..];
        return name;
    }

    /// <summary>"en la Ballena", "en Orión".</summary>
    public static string InConstellation(Core.Constellations.Constellation c) => "en " + Constellation(c);

    /// <summary>"hacia la Ballena", "hacia Orión".</summary>
    public static string TowardsConstellation(Core.Constellations.Constellation c) => "hacia " + Constellation(c);
}
