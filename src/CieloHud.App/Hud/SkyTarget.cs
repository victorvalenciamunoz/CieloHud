using CieloHud.Core.Satellites;
using CieloHud.Core.Sky;
using CieloHud.Core.SolarSystem;

namespace CieloHud.App.Hud;

/// <summary>Something the HUD can guide to.</summary>
public abstract class SkyTarget
{
    protected SkyTarget(string name) => Name = name;

    public string Name { get; }

    /// <summary>Null when the target cannot be located yet (e.g. ISS without elements).</summary>
    public abstract HorizontalPosition? Locate(Observer observer, DateTimeOffset instant);

    /// <summary>Short reason shown when <see cref="Locate"/> returns null.</summary>
    public virtual string? Unavailable => null;
}

public sealed class BodyTarget : SkyTarget
{
    private readonly ISolarSystemService _service;
    private readonly CelestialBody _body;

    public BodyTarget(string name, CelestialBody body, ISolarSystemService service) : base(name)
    {
        _body = body;
        _service = service;
    }

    public override HorizontalPosition? Locate(Observer observer, DateTimeOffset instant) => _service.Locate(_body, observer, instant);
}

public sealed class SatelliteTarget : SkyTarget
{
    private const int IssNoradNumber = 25544;

    private readonly ISatelliteService _service;
    private readonly ITleProvider _tleProvider;
    private Tle? _tle;
    private string? _error;

    public SatelliteTarget(string name, ISatelliteService service, ITleProvider tleProvider) : base(name)
    {
        _service = service;
        _tleProvider = tleProvider;
    }

    public override string? Unavailable => _error ?? (_tle is null ? "descargando órbita…" : null);

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

/// <summary>The fixed list of things worth looking at, in display order.</summary>
public sealed class TargetCatalog
{
    public IReadOnlyList<SkyTarget> Targets { get; }

    public TargetCatalog(ISolarSystemService solarSystem, ISatelliteService satellites, ITleProvider tleProvider)
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
    }
}
