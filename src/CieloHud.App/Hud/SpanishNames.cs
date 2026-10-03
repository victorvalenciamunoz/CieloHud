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

    /// <summary>"la Ballena", "Orión"…; falls back to the Latin name for an unknown symbol.</summary>
    public static string Constellation(Core.Constellations.Constellation c) => Constellations.GetValueOrDefault(c.Symbol, c.Name);

    /// <summary>"en la Ballena", "en Orión".</summary>
    public static string InConstellation(Core.Constellations.Constellation c) => "en " + Constellation(c);

    /// <summary>"hacia la Ballena", "hacia Orión".</summary>
    public static string TowardsConstellation(Core.Constellations.Constellation c) => "hacia " + Constellation(c);
}
