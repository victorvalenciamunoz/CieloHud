using CieloHud.Core.Cards;
using CieloHud.Core.Satellites;
using CieloHud.Core.Sky;
using CieloHud.Core.SolarSystem;
using CieloHud.Core.Stars;

namespace CieloHud.App.Hud;

/// <summary>Something the HUD can guide to or identify.</summary>
public abstract class SkyTarget
{
    protected SkyTarget(string name, string kind)
    {
        Name = name;
        Kind = kind;
    }

    public string Name { get; }

    /// <summary>What it is, in plain words ("planeta", "estrella"…).</summary>
    public string Kind { get; }

    /// <summary>Null when the target cannot be located yet (e.g. ISS without elements).</summary>
    public abstract HorizontalPosition? Locate(Observer observer, DateTimeOffset instant);

    /// <summary>Short reason shown when <see cref="Locate"/> returns null.</summary>
    public virtual string? Unavailable => null;

    /// <summary>Which card it has: its written text and facts (decision 038).</summary>
    public abstract CardKey Card { get; }
}

public sealed class BodyTarget : SkyTarget
{
    private readonly ISolarSystemService _service;

    public BodyTarget(string name, CelestialBody body, ISolarSystemService service)
        : base(name, body == CelestialBody.Moon ? "nuestro satélite" : "planeta")
    {
        Body = body;
        _service = service;
    }

    public CelestialBody Body { get; }

    public override CardKey Card => CardKey.Body(Body);

    public override HorizontalPosition? Locate(Observer observer, DateTimeOffset instant) => _service.Locate(Body, observer, instant);
}

public sealed class StarTarget : SkyTarget
{
    private readonly IStarService _service;

    public StarTarget(string name, Star star, IStarService service) : base(name, "estrella")
    {
        Star = star;
        _service = service;
    }

    public Star Star { get; }

    public override CardKey Card => CardKey.Star(Star);

    public override HorizontalPosition? Locate(Observer observer, DateTimeOffset instant) => _service.Locate(Star, observer, instant);
}

public sealed class SatelliteTarget : SkyTarget
{
    private const int IssNoradNumber = 25544;

    private readonly ISatelliteService _service;
    private readonly ITleProvider _tleProvider;
    private Tle? _tle;
    private string? _error;

    public SatelliteTarget(string name, ISatelliteService service, ITleProvider tleProvider) : base(name, "estación espacial")
    {
        _service = service;
        _tleProvider = tleProvider;
    }

    public override string? Unavailable => _error ?? (_tle is null ? "descargando órbita…" : null);

    public override CardKey Card => CardKey.Iss;

    public override HorizontalPosition? Locate(Observer observer, DateTimeOffset instant) =>
        _tle is { } tle ? _service.Locate(tle, observer, instant) : null;

    /// <summary>Fetches the elements once; safe to call again to retry.</summary>
    public async Task PrepareAsync(CancellationToken cancellationToken = default)
    {
        if (_tle is not null)
            return;
        try
        {
            _tle = await _tleProvider.GetTleAsync(IssNoradNumber, cancellationToken);
            _error = null;
        }
        catch (TleUnavailableException)
        {
            _error = "sin órbita (sin red)";
        }
    }
}

/// <summary>
/// What the app knows about: <see cref="Targets"/> can be chosen and guided to; <see cref="All"/> adds the bright stars,
/// drawn as references and used to answer "what is that?".
/// </summary>
public sealed class TargetCatalog
{
    public IReadOnlyList<SkyTarget> Targets { get; }
    public IReadOnlyList<SkyTarget> All { get; }

    public TargetCatalog(ISolarSystemService solarSystem, ISatelliteService satellites, ITleProvider tleProvider, IStarService stars)
    {
        Targets =
        [
            new BodyTarget("Luna", CelestialBody.Moon, solarSystem),
            new BodyTarget("Venus", CelestialBody.Venus, solarSystem),
            new BodyTarget("Marte", CelestialBody.Mars, solarSystem),
            new BodyTarget("Júpiter", CelestialBody.Jupiter, solarSystem),
            new BodyTarget("Saturno", CelestialBody.Saturn, solarSystem),
            new BodyTarget("Mercurio", CelestialBody.Mercury, solarSystem),
            new SatelliteTarget("ISS", satellites, tleProvider),
        ];

        All =
        [
            .. Targets,
            .. BrightStars.All.Select(s => new StarTarget(SpanishNames.Star(s), s, stars)),
        ];
    }
}
