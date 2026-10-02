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

### Paso 4 — Satélites (2026-10-02)

- Paquete `SGP.NET` 1.6.0 en Core (decisión 004). Carpeta `Satellites/`.
- `Tle` (`record struct`): nombre, dos líneas verbatim, número NORAD y época UTC parseados; `Tle.Parse` acepta el formato de 3 líneas de CelesTrak. Valida longitud (69) y número de línea.
- `ISatelliteService.Locate(tle, observer, instant)` e implementación `Sgp4SatelliteService`. Altura geométrica (sin refracción): los satélites solo interesan bien por encima del horizonte.
- Tests con TLE fijo de la ISS (CelesTrak, época 2026-10-01 19:41 UTC) frente a **JPL Horizons** (`-125544`, sin atmósfera) en dos pasos reales sobre Madrid:
  2026-10-02 11:26 UTC (alt 47°) y 17:56 UTC (alt 32°), más un instante bajo el horizonte. Tolerancia 0,1° / 0,5 %.
- Comparación medida: acimut 0,002°-0,008°, altura 0,0003°, distancia 567,41 km frente a 567,35 km. El PLAN pedía < 1°.
- Validado con `dotnet build` (0 avisos) y `dotnet test` (61 tests).

### Paso 5 — Proveedor de TLE (2026-10-02)

- `ITleProvider.GetTleAsync(norad)` con dos implementaciones: `FixedTleProvider` (TLE dado; tests y uso manual) y
  `CelesTrakTleProvider` (descarga de `celestrak.org/NORAD/elements/gp.php?CATNR=…&FORMAT=TLE` y caché en fichero; decisión 007).
- Caché: fichero `tle-{norad}.txt` con hora de descarga + 3 líneas; se reutiliza si tiene < 24 h; si falla la red se devuelve la copia vieja;
  sin copia ni red, `TleUnavailableException`. Ruta de la caché y `HttpClient` inyectados; reloj del sistema.
- Tests sin red con HTTP falso y ficheros de caché sembrados con la fecha de descarga que toque: sin caché, caché fresca, caché caducada, caducada + red caída, sin caché + red caída,
  respuesta "No GP data found", caché corrupta, formato del fichero.
- Comprobación manual contra CelesTrak real: primera llamada 1,3 s (descarga y escribe caché), segunda 23 ms sin red. Época del TLE recibido: 2026-10-01 19:41 UTC.
- Validado con `dotnet build` (0 avisos) y `dotnet test` (71 tests).

### Paso 6 — Consola (2026-10-02)

- `dotnet run --project src/CieloHud.Console -- [--lat] [--lon] [--alt] [--time] [--help]`. Por defecto Madrid (40.4168, -3.7038, 650 m) y ahora.
  `--time` en ISO-8601; sin desfase se lee como UTC. Números con punto decimal (cultura invariante) para comparar con Stellarium/Horizons.
- Salida: cabecera con observador e instante (UTC y hora local), tabla con Luna, Mercurio, Venus, Marte, Júpiter, Saturno e ISS:
  acimut, punto cardinal en español (O = Oeste), altura, "sobre/bajo el horizonte", distancia. Al final, época y edad del TLE.
- ISS: `CelesTrakTleProvider` con caché en `%LocalAppData%\CieloHud\tle-25544.txt`. Sin red ni caché, la fila ISS dice "sin TLE (sin red)" y el resto se muestra igual.
- Ficheros: `CommandLineOptions` (parseo y ayuda), `SkyTableFormatter` (solo formato), `Program` (orquesta). Sin cálculo en la consola.
- Verificado a mano: la tabla a las 2026-10-02 11:26 UTC da ISS 334.63° / 47.35° y a las 21:00 UTC Saturno 118.11° / 31.71°, los mismos valores que los tests contra Horizons.
  Argumento inválido → mensaje + ayuda + código 1; `--help` → ayuda + código 0.

## Pendiente (Fase 1)

7. Validación contra Stellarium en al menos 3 instantes; resultados aquí.
