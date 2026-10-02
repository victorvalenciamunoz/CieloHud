# Estado

Fase actual: 2 (ver PLAN.md). Fase 1 completada el 2026-10-02.

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

### Paso 7 — Validación contra Stellarium (2026-10-02)

Stellarium Web, ubicación Humanes de Madrid (en CieloHud: 40.2525, -3.8278, 620 m; Stellarium no muestra sus coordenadas exactas), hora local UTC+2.

| Hora local | Objeto | Stellarium Az / Alt | CieloHud Az / Alt | Diferencia |
|---|---|---|---|---|
| 02/10 14:23:34 | Luna | 302.685° / 4.426° | 302.678° / 4.435° | 0.007° / 0.009° |
| 02/10 23:00:00 | Luna | 44.581° / -7.468° | 44.553° / -7.490 (geométrica) | 0.028° / 0.022° |
| 03/10 06:00:00 | Luna | 110.945° / 63.720° | 110.942° / 63.720° | 0.003° / 0.000° |

- Distancias: 368 911,6 km frente a 368 916,7 km (14:23) y 364 061,3 km frente a 364 085,9 km (06:00): < 0,01 %.
- Bajo el horizonte Stellarium no aplica refracción; se compara con la altura geométrica. Irrelevante para el uso real.
- Lección: el reloj de Stellarium Web corre en tiempo real; hay que pausarlo antes de leer. Una lectura con 60 s de desfase dio 0,16° en la Luna cerca del horizonte.
- Luna, Júpiter y Saturno ya se habían contrastado con JPL Horizons (paso 3) y la ISS con Horizons en dos pasos reales (paso 4): todo < 0,01°.

## Fase 1 — Hecho

- `dotnet test`: 71 tests en verde. `dotnet build`: 0 avisos.
- Tabla de la consola contrastada con Stellarium en 3 instantes y con JPL Horizons en 5 (planetas, Luna, ISS), todo dentro de tolerancia (< 0,5° / < 1°) con margen de dos órdenes de magnitud.
- Pendiente opcional: lecturas de Marte/Júpiter/Saturno en Stellarium para completar la tabla (ya validados con Horizons).

## Fase 2 — Próximos pasos visibles de la ISS

### Paso 1 — Sol desde el observador (2026-10-02)

- `ISunService.Locate(observer, instant)` e implementación `AstronomyEngineSunService`, en `SolarSystem/`. Altura **geométrica**, sin refracción (decisión 008): los crepúsculos se definen así.
- Refactor: `AstronomyEngineLocator` (interno) comparte la llamada a Astronomy Engine entre planetas (refracción normal) y Sol (sin refracción).
- Tests contra JPL Horizons (Sol, Madrid, sin atmósfera): día (12:00 UTC, alt 45.9°), crepúsculo (18:30 UTC, alt -7.4°) y noche (21:00 UTC, alt -34.8°). Tolerancia 0,05°.
- Validado con `dotnet build` (0 avisos) y `dotnet test` (75 tests).

### Paso 2 — Geometría de pasos (2026-10-02)

- Carpeta `Passes/`: `PassPoint` (instante + posición), `SatellitePass` (inicio, máximo, fin; `Duration`, `MaxAltitudeDegrees`),
  `ISatellitePassPredictor.Predict(tle, observer, desde, hasta)` y `Sgp4SatellitePassPredictor` (decisión 009).
- `SgpConversions` (interno) comparte con `Sgp4SatelliteService` las conversiones a tipos de SGP.NET.
- Tests contra JPL Horizons (ISS, Madrid, `R_T_S_ONLY='GEO'`, paso 1 min) para el 2026-10-01 20:00 → 2026-10-02 20:00 UTC:
  7 pasos encontrados, salidas y puestas dentro de ±60 s de Horizons, máximos dentro de ±60 s y altura máxima compatible con la muestra.
  Más tests de recorte en `desde`, paso completo aunque acabe tras `hasta`, instantes en UTC, rango vacío.
- Medido: salidas 09:44:19, 11:20:27, 12:58:30, 14:36:34, 16:13:35, 17:50:29, 19:30:04 UTC (Horizons: 09:45, 11:21, 12:59, 14:37, 16:14, 17:51, 19:30 al minuto siguiente).
  Máximo del paso de las 11:25:54: 47,54°; Horizons muestreó 47,35° a las 11:26.
- Validado con `dotnet build` (0 avisos) y `dotnet test` (88 tests).

### Paso 3 — Iluminación de la ISS (2026-10-02)

- `Satellites/`: `EciPosition`, `EarthShadow.IsInUmbra(satélite, sol)` (umbra cónica, decisión 010), `ISatelliteIlluminationService.IsSunlit(tle, instant)`
  y `Sgp4SatelliteIlluminationService` (ISS y Sol en ECI de SGP.NET).
- Tests geométricos de la sombra: lado diurno, detrás de la Tierra, terminador, borde del cilindro, estrechamiento del cono, normalización del Sol.
- Tests de integración: ISS iluminada sobre Madrid a mediodía y en el paso vespertino (Sol a -0,9°); comprobación física independiente:
  en una órbita de 93 min hay exactamente un eclipse, del 20-45 % del periodo. Medido: 33,9 %, un tramo de 31,5 min (01:01-01:32 UTC del 2 de octubre).
- Observación: entre el 2 y el 4 de octubre todos los pasos sobre Madrid (incluido uno rasante con el Sol a -19°) son con la ISS iluminada; no hay pasos nocturnos profundos en esas fechas.
- Validado con `dotnet build` (0 avisos) y `dotnet test` (101 tests).

### Pendiente (Fase 2)

4. Visibilidad: combinar Sol + paso + iluminación; tramo visible; tests con servicios falsos.
5. Consola: `--passes N` con tabla tipo Heavens-Above.
6. Validación contra Heavens-Above; resultados aquí.
