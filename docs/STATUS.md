# Estado

Fase actual: 1 (ver PLAN.md).

## Hecho

### Paso 1 — Esqueleto (2026-10-02)

- `CieloHud.slnx` con tres proyectos en .NET 10: `src/CieloHud.Core`, `src/CieloHud.Console`, `tests/CieloHud.Core.Tests`.
- `global.json` fija el SDK 10.0.401 (`rollForward: latestPatch`) para no usar el .NET 11 preview instalado en la máquina.
- `.gitignore` estándar de .NET.
- Validado con `dotnet build` (0 avisos, 0 errores) y `dotnet test` (1 test humo que carga el ensamblado de Core).

### Paso 2 — Tipos base (2026-10-02)

- `Observer` (lat, lon, altitud en m) y `HorizontalPosition` (acimut, altura, distancia opcional en km) como `readonly record struct` con validación de rangos.
- `Azimuth.Normalize` (envuelve a [0, 360)) y `Azimuth.ToCardinalPoint` (8 puntos, sectores de 45° centrados en cada punto; los nombres en español son cosa de la UI).
- `CardinalPoint` y `CelestialBody` (Moon, Mercury, Venus, Mars, Jupiter, Saturn).
- `HorizontalPosition.IsAboveHorizon` = altura > 0 (solo geometría; no dice nada de si se ve).
- Validado con `dotnet build` (0 avisos) y `dotnet test` (45 tests: límites de sectores, normalización, validación de rangos). Eliminado el test humo del paso 1.

### Paso 3 — Sistema solar (2026-10-02)

- Paquete `CosineKitty.AstronomyEngine` 2.1.19 en Core (decisión 003).
- `ISolarSystemService.Locate(body, observer, instant)` e implementación `AstronomyEngineSolarSystemService`:
  posición topocéntrica, aberración corregida, época de la fecha, refracción normal (decisión 005). Devuelve también la distancia en km.
- Tests con valores de referencia de **JPL Horizons** (decisión 006) para Madrid:
  Luna y Júpiter el 2026-10-03 04:00 UTC, Saturno el 2026-10-02 21:00 UTC. Tolerancia 0,05° / 0,1 %.
- Comparación medida frente a Horizons (sobre el horizonte): acimut y altura difieren entre 0,0004° y 0,003°; distancia < 0,01 %.
  Bajo el horizonte (Júpiter a -33°) la altura refractada difiere 0,24°: cada fuente aplica una refracción distinta ahí; irrelevante porque no se ve.
- Test de que un `DateTimeOffset` con desfase local da el mismo resultado que su equivalente UTC.
- Validado con `dotnet build` (0 avisos) y `dotnet test` (50 tests).

### Organización de Core (2026-10-02)

- Carpeta por feature, con namespace igual a la carpeta: `Sky/` (Observer, HorizontalPosition, Azimuth, CardinalPoint),
  `SolarSystem/` (CelestialBody, ISolarSystemService, AstronomyEngineSolarSystemService). `Satellites/` llegará en el paso 4.
  Los tests siguen la misma estructura y tienen `using` globales de esos namespaces en el csproj.

## Pendiente (Fase 1)

4. Satélites con `SGP.NET`; test de la ISS con TLE fijo.
5. Proveedor de TLE (CelesTrak + caché 24 h en fichero; implementación fija para tests).
6. Consola con `--lat/--lon/--time` y tabla.
7. Validación contra Stellarium en al menos 3 instantes; resultados aquí.
