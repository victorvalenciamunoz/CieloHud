# Estado

Fase actual: 4 (ver PLAN.md). Fases 1 y 2 completadas el 2026-10-02; fase 3 el 2026-10-05.

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

### Paso 4 — Visibilidad (2026-10-02)

- `Passes/`: `VisibilityCriteria` (Sol ≤ -6°, máximo ≥ 10°, paso 10 s), `VisiblePass` (paso geométrico + tramo visible: inicio, máximo, fin;
  `EndsInShadow`, `StartsFromShadow`), `IVisiblePassFinder.Find(tle, observer, desde, hasta)` y `VisiblePassFinder` (decisión 011).
- Tests unitarios con servicios falsos (paso sintético de 10 min con máximo a 40°): noche, día, crepúsculo en el umbral, entrada en sombra a mitad,
  salida de sombra a mitad, siempre en sombra, paso bajo con criterios relajados, sombra antes de alcanzar los 10°.
- Tests de integración con servicios reales: 2-4 oct nada visible sobre Madrid (todo de día o rasante); con máximo mínimo 0° aparece el rasante
  del 2 oct 19:30 UTC; en dos semanas, cada paso devuelto cumple todos los criterios en sus tres puntos y las banderas cuadran con la iluminación.
- Fallo corregido durante el paso: descartaba muestras con altura < 0, y los extremos del paso geométrico tienen ±0,05° por su resolución de 1 s,
  lo que acortaba el tramo y falseaba las banderas.
- Sondeo 2-16 oct (TLE del 1 oct): 3 pasos visibles, todos de madrugada saliendo de la sombra: 14 oct 05:08 UTC (máx 21°), 15 oct 04:24 (13°), 16 oct 05:12 (67°). 290 ms para 14 días.
- Validado con `dotnet build` (0 avisos) y `dotnet test` (114 tests).

### Paso 5 — Consola: pasos visibles (2026-10-02)

- `--passes <días>` (1-30) lista los pasos visibles de la ISS desde `--time` (por defecto ahora) en lugar de la tabla de posiciones.
  Tabla tipo Heavens-Above en hora local: fecha; inicio, máximo y fin con hora, altura y acimut (punto cardinal en español); duración del tramo visible;
  nota si "aparece a media altura (sale de la sombra)" o "se apaga antes de ponerse (entra en sombra)". Cabecera con el criterio aplicado y pie con la época del TLE.
- `PassTableFormatter` (solo formato); `Program` elige entre las dos tablas. Sin TLE (sin red ni caché) en modo pasos → error y código 2.
- Verificado a mano: `--passes 14` desde el 2 oct da los tres pasos del sondeo del paso 4 (14, 15 y 16 oct de madrugada); `--passes 2` da "(ninguno)";
  `--passes 0` → mensaje + ayuda + código 1; la tabla de posiciones sigue igual.

### Paso 6 — Validación contra Heavens-Above (2026-10-02)

Heavens-Above con ubicación Humanes de Madrid (40.2509, -3.8271, 0 m; mismos valores en CieloHud), hora local UTC+2, periodo 12-22 oct, "sólo los visibles".
TLE de CieloHud del 1 oct 19:41 UTC; Heavens-Above usa uno del 2 oct. Heavens-Above corta sus tablas a 10° de altura; CieloHud al horizonte,
así que se compara el máximo y, cuando la ISS aparece saliendo de la sombra, el punto de aparición.

| Fecha | Heavens-Above (máximo) | CieloHud (máximo) | Dif. | Aparición HA / CieloHud |
|---|---|---|---|---|
| 13 oct | 8:00:09 37° SE | 8:00:09 37° SE | 0 s | 10° SSO (corte) / 0° SO |
| 14 oct | 7:13:02 22° SE | 7:13:01 22° SE | 1 s | 10° S (corte) / 2° S |
| 15 oct | 6:25:56 13° SE | 6:25:55 13° SE | 1 s | 6:24:24 10° SSE / 6:24:21 10° SE |
| 16 oct | 7:15:05 68° SE | 7:15:04 67° SE | 1 s, 1° | 7:12:53 20° SO / 7:12:47 19° SO |
| 17 oct | 6:28:07 37° SE | 6:28:01 37° SE | 6 s | 37° SE / 37° SE |
| 18 oct | 5:43:12 11° E | 5:43:08 11° E | 4 s | 11° E / 11° E |
| 18 oct | 7:17:20 42° NNO | 7:17:19 42° NO | 1 s | 7:16:12 30° O / 7:16:15 31° O |
| 19 oct | 6:31:10 37° NE | 6:31:11 36° NE | 1 s, 1° | 37° NE / 36° NE |
| 20 oct | 7:19:48 21° NNO | 7:19:46 21° N | 2 s | 7:19:01 20° NO / 7:18:59 20° NO |
| 21 oct | 6:33:47 21° NNE | 6:33:41 21° N | 6 s | 21° NNE / 21° N |

- 10 pasos coincidentes con el máximo a ≤ 6 s y ≤ 1°, y el punto de aparición desde la sombra a ≤ 6 s y 1°. Geometría, sombra y Sol validados.
- Casos en el filo del corte de -6° (pasos de madrugada que empiezan con el Sol entre -6,0° y -6,3° y acaban a -4,7°):
  CieloHud lista 11 oct 7:58 (12°), 13 oct 8:00 (37°) y 15 oct 8:02 (71°); Heavens-Above lista 13 oct 8:00 y 17 oct 8:04 (29°).
  El 13 y el 15 son numéricamente idénticos (Sol -6,32° al salir, -5,90° al cruzar 10°) y Heavens-Above lista uno y no el otro:
  son decisiones de ruido numérico en el filo, no reproducibles con ningún umbral. Heavens-Above documenta en su FAQ el corte de -6°.
- Hallazgo que cambió el algoritmo (decisión 011): exigir Sol ≤ -6° en cada instante truncaba estos pasos al cruzar -6° y los descartaba;
  ahora se exige al aparecer y el tramo sigue mientras la ISS esté iluminada.
- Heavens-Above lista el 20 oct 5:46:01 (máximo 10°, 5 s); CieloHud lo excluye porque culmina justo por debajo de 10°. Caso límite.
- Validado con `dotnet build` (0 avisos) y `dotnet test` (118 tests).

## Fase 2 — Hecho

- `dotnet test`: 118 tests en verde. `dotnet build`: 0 avisos.
- Pasos visibles de la ISS contrastados con Heavens-Above: 10 de 10 coincidentes a segundos; las únicas diferencias son casos en el filo del corte de -6° que Heavens-Above tampoco resuelve de forma consistente.
- Geometría de pasos contrastada con JPL Horizons (7 de 7 pasos, ±60 s); iluminación con un test físico (un eclipse por órbita, 34 % del periodo).

## Fase 3 — HUD en MAUI (Android)

Entorno comprobado el 2026-10-02: workloads MAUI (android, ios, maccatalyst, maui-windows), SDK Android (API 35-37), Java 17, VS 2022/18.
Móvil de pruebas: OPPO CPH2699, Android 16 (API 36), con sensores Rotation Vector, Geomagnetic Rotation Vector y Game Rotation Vector.
Estética: la de GincanaHud (fondo `#0B1218`, menta `#7CFFB2`, cian `#4DD2FF`, ámbar `#FFC42E`, rojo `#FF5C5C`, texto `#C5D0DB`/`#E8EEF4`; fuentes Chakra Petch y Open Sans).
Solo orientación vertical.

### Paso 1 — Geometría de guiado (2026-10-02)

- `Guidance/`: `PointingDirection` (hacia dónde apunta el móvil), `GuidanceSettings` (zona de objetivo con histéresis: entra a 4°, sale a 6°),
  `Guidance` (giro de acimut con signo en (-180, 180], positivo = derecha; diferencia de altura, positivo = subir; distancia angular; en objetivo)
  y `GuidanceCalculator.Compute(pointing, target, wasOnTarget)`. Sin estado; el llamador pasa el estado anterior.
- Tests: giro más corto con envoltura (350→10 = +20), caso 180°, distancia angular por fórmula del haversine (horizonte, cénit, cerca del cénit),
  histéresis en los dos sentidos, validación de ajustes.
- Validado con `dotnet build` (0 avisos) y `dotnet test` (147 tests).

### Paso 2 — Orientación y suavizado (2026-10-02)

- `Guidance/OrientationMath`: cuaternión dispositivo→mundo (convención Android) → `PointingDirection` del eje -Z (cámara trasera); `RollDegrees` (giro de pantalla). Decisión 012.
- `Guidance/PointingSmoother`: suavizado exponencial con el acimut en el círculo (350°→10° pasa por 0°).
- Tests con poses conocidas: plano en la mesa (apunta a -90°), vertical mirando al norte (0°, 0°), guiñadas de ±90°/180°, inclinaciones (±30°, cénit), roll de 20°, cuaterniones sin normalizar, vectores mundo directos. Suavizado: primera lectura, fracción, cruce de norte en ambos sentidos, convergencia, reset.
- Validado con `dotnet build` (0 avisos) y `dotnet test` (174 tests).

### Paso 3 — Proyecto MAUI (2026-10-02)

- `src/CieloHud.App`: plantilla `maui` recortada a `net10.0-android` (las carpetas de iOS/Mac/Windows quedan en el repo pero no se compilan),
  `ApplicationId` `com.cielohud.app`, Android mínimo API 26, orientación fija en vertical (`MainActivity`), tema oscuro forzado,
  `EmbedAssembliesIntoApk` en Debug (evita un crash conocido del Fast Deployment con ids de recursos desfasados; heredado de GincanaHud).
- Permisos en el manifiesto: ubicación (fina y gruesa), internet; GPS/brújula/acelerómetro declarados como no obligatorios.
- Recursos: paleta `Hud*` en `Colors.xaml` (misma que GincanaHud), fuentes Chakra Petch (regular/bold) y Open Sans registradas en `MauiProgram`.
- Referencia a `CieloHud.Core`. `MainPage` es un marcador de posición ("CieloHud · Fase 3 · esqueleto").
- Validado: `dotnet build src/CieloHud.App -f net10.0-android` en verde (4 min la primera vez, 0 avisos); `dotnet test` 174 tests;
  instalada con `-t:Install` y arrancada en el OPPO CPH2699 (Android 16): sin crash, captura de pantalla con el diseño esperado.
- Comandos útiles: `adb` está en `%LocalAppData%\Android\Sdk\platform-tools`; captura: `adb exec-out screencap -p > captura.png`.
  Si cambian las fechas de muchos ficheros (p. ej. al reescribir el historial) y la app se cae al arrancar: desinstalarla y borrar `bin/` y `obj/` de la app.

### Paso 4 — Página de diagnóstico (2026-10-02)

- `Services/`: `IPointingSource` + `OrientationSensorPointingSource` (sensor de orientación de MAUI → `OrientationMath` → declinación → `PointingSmoother`),
  `ILocationSource` + `GeolocationSource` (permiso + última posición + fix fresco), `MagneticDeclination` (`GeomagneticField` de Android).
- `DiagnosticsPage`: GPS y precisión, declinación, hacia dónde apunta el móvil (acimut, altura, roll, sin suavizar), posición del Sol y de la Luna
  y las indicaciones de `GuidanceCalculator` para cada uno ("izquierda 106°, sube 75°" / "¡AQUÍ!"). Sol y Luna se recalculan cada segundo; la UI de orientación a 10 Hz.
- Servicios de Core y de dispositivo registrados en `MauiProgram`; la página se resuelve por inyección.
- Verificado en el OPPO: GPS con precisión de ±13 m, declinación +0,7°, Sol a 230,2° SO / 32,5° a las 16:46 local, Luna a -13,8° (puesta a las 14:55, coincide con Stellarium).
  Pendiente: prueba real apuntando al Sol (el usuario), que valida la cadena sensor → cielo.

### Paso 5 — HUD (2026-10-02)

- Core `Guidance/HudProjection`: indicación → punto de pantalla (escala fija de grados por píxel según campo de visión de 60°, acimut acortado por cos(altura);
  fuera de vista se recorta al borde conservando la dirección). Tests.
- App `Hud/`: `SkyTarget` (`BodyTarget` para Luna y planetas, `SatelliteTarget` para la ISS con descarga del TLE bajo demanda), `TargetCatalog`,
  `HudFrame` (datos de un fotograma) y `HudDrawable` (retícula con pulso al fijar, marcador en rombo con esquinas, flecha de borde, texto en palabras
  "derecha 170° · sube 54°" / "AQUÍ · Baja el móvil y mira justo ahí", estado "bajo el horizonte", mensajes de espera).
- `HudPage`: chips de objetivo (Luna, Venus, Marte, Júpiter, Saturno, Mercurio, ISS), lienzo a 30 fps, pie con posición y declinación, botón a Diagnóstico.
  Es la página inicial; `DiagnosticsPage` queda como ruta.
- DI: `HttpClient`, `CelesTrakTleProvider` con caché en `FileSystem.AppDataDirectory/tle`, `Sgp4SatelliteService`, `TargetCatalog`.
- Verificado en el OPPO: captura con Venus seleccionado fuera de vista, flecha en el borde hacia arriba-derecha coherente con el texto. 182 tests en verde.
- Pendiente de validar en el cielo (paso 7): esta noche Saturno (23:00, SE, 31°) y la Luna (desde las 00:30, ENE).
- Limitaciones conocidas: el roll de la pantalla no se compensa (solo vertical); sin aviso de calibración del magnetómetro (paso 6).

### Paso 5b — Referencias en el HUD (2026-10-02)

- Sin cámara, por diseño (VISION: el móvil guía, los ojos miran). Para que la pantalla "se mueva contigo": línea del horizonte con escala de altura cada 10°,
  brújula con puntos cardinales (N en rojo) y marcas cada 10° a lo largo del horizonte (pegada abajo si el horizonte sale de pantalla),
  y los demás objetos del catálogo como puntos cian con nombre (grises si están bajo el horizonte). Las posiciones del cielo se recalculan una vez por segundo.
- Idea futura acordada: opción para elegir entre referencias dibujadas o cámara de fondo (requeriría `CommunityToolkit.Maui.Camera` y permiso de cámara).
- Verificado en el OPPO por el usuario: "superchulo".

### Paso 6 — Ruido del magnetómetro (2026-10-02)

- Prueba de precisión sin cielo (llovía): punto de referencia en tierra, a unos 400 m, con acimut conocido calculado desde coordenadas de mapa:
  acimut de referencia **58,6°** (±0,7°). Apuntando con el móvil como mira (objeto tapado por el centro de la pantalla):
  primera lectura **43°** y saltando (magnetómetro descalibrado); tras un "8" en el aire, **53°**; en una tercera medición, **56°** (error 2,6°).
  Conclusión: convenciones de ejes y signos correctas; el error residual es el del sensor, dentro de la zona de 4°/6° de `GuidanceSettings`.
- `RotationVectorPointingSource` (Android, `SensorManager` directo): mismo cuaternión que el sensor de MAUI pero con la **precisión** del sensor
  (`Unreliable/Low/Medium/High`). `IPointingSource.Accuracy` y `AccuracyChanged`; la implementación de MAUI queda como reserva portátil sin precisión.
- HUD: banner rojo "BRÚJULA SIN CALIBRAR · mueve el móvil dibujando un 8" cuando la precisión es baja o no fiable; diagnóstico muestra la precisión.
- Suavizado: alfa de 0,2 a **0,08** (`PointingSmoothing.Alpha`), ~medio segundo de respuesta a 50 Hz; antes bailaba.
- Validado: build Android 0 avisos, 182 tests, instalado y arrancado sin errores en el OPPO.

### Paso 8 — Catálogo de estrellas brillantes (2026-10-03)

- Core `Stars/`: `Star` (nombre IAU, designación Bayer, AR/Dec J2000, magnitud), `BrightStars` (20 estrellas con V < 1,65 visibles desde Madrid + Polaris, datos de SIMBAD),
  `IStarService` y `AstronomyEngineStarService` (precesión/nutación a la fecha y horizonte con refracción). Decisión 013.
- Tests: Polaris a la altura de la latitud (±0,75°) en cuatro horas del día; culminación de Vega, Sirio, Arturo, Antares, Deneb y Capella a 90° − |lat − dec|,
  por el sur o por el norte (Deneb y Capella, con declinación mayor que la latitud, culminan al norte del cénit). Unicidad y rangos del catálogo.
- Error corregido en el propio test durante el paso: asumía culminación al sur para todas; y el muestreo por minutos no basta cerca del cénit (Vega), se afina a segundos.
- Validado con `dotnet test` (195 tests).

### Paso 9 — Modo "¿qué es eso?" y estrellas en el HUD (2026-10-03)

- Core `Guidance/SkyIdentifier`: `Identify` (candidato más cercano a donde apuntas, si está a ≤ 5°; ignora lo que está bajo el horizonte) y `Nearest` (el más cercano a cualquier distancia).
  Tests: acierto, nada en el radio, bajo el horizonte ignorado/incluible, el más cercano gana, cruce del norte, radio configurable.
- App: `StarTarget` y nombres en español de las 21 estrellas; `TargetCatalog.All` = objetivos seleccionables + estrellas. Cada objetivo tiene un "tipo" en palabras
  (planeta, estrella, nuestro satélite, estación espacial).
- HUD: chip "¿QUÉ ES?" al principio de la barra. En ese modo la retícula se ilumina cuando hay algo a ≤ 5° y muestra su nombre en grande con tipo, altura y distancia
  al centro; si no, "Nada conocido aquí · Lo más cercano: Vega, 12° a la derecha y más arriba". La ISS se descarga también en este modo.
- Referencias: las estrellas se dibujan en blanco con tamaño según su magnitud; Luna, planetas e ISS en cian.
- Validado: build Android 0 avisos, 204 tests. Pendiente instalarlo en el móvil (estaba desconectado) y probarlo en el cielo.

### Paso 10 — Constelaciones (2026-10-03)

- Core `Constellations/`: `Constellation` (símbolo IAU, nombre latino), `IConstellationLocator` y `AstronomyEngineConstellationLocator`
  (dirección aparente → J2000 → límites oficiales de la IAU). Decisión 014.
- Fallo de Astronomy Engine 2.1.19 encontrado y esquivado: `VectorFromHorizon` con refracción normal no termina nunca en altura 90° o -71°
  (apuntar al cénit habría congelado la app). Se quita la refracción con la fórmula directa. Barrido de 34.608 direcciones sin bloqueos.
- Tests: 10 estrellas en su constelación de Bayer; Saturno (Ballena), Júpiter (Leo), Marte (Cáncer), Venus (Virgo) y Luna (Géminis) contra JPL Horizons;
  la esfera completa cubre las 88 constelaciones; alturas extremas con límite de tiempo.
- App: `SpanishNames` con las 88 constelaciones en español con artículo ("la Ballena", "Orión"), comprobadas contra los 88 símbolos de la librería,
  y los nombres españoles de estrellas. HUD: en "¿QUÉ ES?", "Estás mirando hacia Orión" siempre, y "planeta · en la Ballena · altura 31°" al acertar;
  en modo guía, "Saturno, en la Ballena · acimut…".
- VISION: pocos objetos que buscar, más cosas que reconocer.
- Validado: 227 tests; build Android 0 avisos; instalada y arrancada sin errores.

### Paso 11 — Estrellas hasta magnitud 3 (2026-10-03)

- `BrightStars`: 155 estrellas generadas desde SIMBAD (V < 3,05) y el catálogo de nombres de la IAU cruzado por posición. 130 con nombre propio;
  las otras 25, por designación de Bayer. `Star.ProperName` opcional; `BrightStars.Get` busca por nombre o por designación. Decisión 015.
- `SkyIdentifier`: preferencia por la más brillante (1° por magnitud); `SkyCandidate.Magnitude`. La distancia que se informa sigue siendo la real.
- App: "Gamma de Casiopea", "Alfa del Lobo", "Ómicron 2 del Can Mayor"… (comprobados los 25); solo se rotulan las de magnitud < 1,7.
- Tests: 155 estrellas, designaciones y nombres únicos, sin dos estrellas en la misma posición, orden por brillo, búsqueda por nombre y por designación,
  y cuatro casos de preferencia por brillo (la brillante gana si la débil solo está un poco más cerca; la débil gana si está claramente centrada;
  los planetas no se penalizan; el brillo nunca mete algo de fuera del radio).
- Validado: 236 tests; build Android 0 avisos. Pendiente instalar (móvil desconectado).

### Paso 12 — Figuras de constelaciones y proyección gnomónica (2026-10-04)

- Core `HudProjection`: proyección gnomónica (como una cámara) en lugar de la aproximación plana; `ToScreen` (null si está detrás) y `Project`
  (al borde con dirección, también para lo que está detrás). Decisión 016. Tests: centro, tangentes, objeto al otro lado del cénit,
  un círculo máximo sale recto, detrás sin posición, flechas a derecha/arriba, recorte en diagonal.
- Core `Constellations/ConstellationFigures`: 88 figuras de d3-celestial generadas a C# (150 trazos, 893 vértices), `IConstellationFigureLocator`
  y `J2000Sky`, el camino J2000 → horizonte compartido con las estrellas. Decisión 017. Tests: 88 figuras completas, la Serpiente en dos trozos,
  y las figuras de Orión, Osa Mayor, Casiopea, Cisne, Lira, Osa Menor y Escorpio pasan a < 0,02° de sus estrellas del catálogo de SIMBAD.
- HUD: todo con la misma proyección (horizonte, escala de alturas, brújula, referencias, marcador); figura de la constelación bajo la retícula en cian tenue
  con su nombre junto al vértice más alto. Se recalcula al cambiar de constelación o una vez por segundo.
- Validado: 253 tests; build Android 0 avisos. En el OPPO: la "tetera" de Sagitario reconocible apuntando casi al nadir (-85°), y el Dragón con su cabeza
  cerca del horizonte norte ("Hacia el Dragón · cerca: Pherkad"). Sin errores en el registro.
- Incidencia: tras reescribir el historial la app se caía al arrancar (`IllegalArgumentException … Theme.MaterialComponents` al crear la barra del `Shell`),
  también en `main`. Causa: compilación incremental con recursos de Android desfasados. Solución: desinstalar y borrar `bin/` y `obj/` de la app.
- Detalles estéticos pendientes: la etiqueta de una estrella puede pisar "HORIZONTE" en el borde izquierdo, y la letra de la brújula queda medio tapada
  por el panel de texto cuando el horizonte cae a su altura.

### Paso 13 — Integración continua y retoques del HUD (2026-10-05)

- `.github/workflows/ci.yml`: en cada PR a `main` restaura, compila en Release y pasa los tests de Core en Ubuntu con el SDK de `global.json`.
  Obligatorio para fusionar (protección de `main`). La app MAUI se sigue compilando y probando a mano. Decisión 018. Insignia de CI en el README.
- HUD: los rótulos de la escala de alturas ("HORIZONTE", "20°"…) se dibujan después de estrellas y planetas, sobre una franja oscura, para que
  ningún nombre los tape; la brújula no baja nunca por detrás del panel de texto inferior (se queda justo encima).
- Validado: 253 tests en Release; build Android 0 avisos. Pendiente: prueba visual en el móvil.

### Paso 7 — Validación en el cielo (2026-10-05)

- Por la mañana, con el cielo despejado, el usuario usó la app en el móvil y el HUD le guió correctamente hasta la **Luna**, el objeto más visible en ese
  momento: al llegar a "AQUÍ" y bajar el móvil, la Luna estaba donde indicaba. Era la prueba pendiente para cerrar la fase.
- Junto con la prueba en tierra del paso 6 (acimut con 2,6° de error tras calibrar la brújula), confirma la cadena completa: sensor de orientación →
  posición del objeto → guía en pantalla.
- Pendiente para cuando se pueda (no bloquea): repetir con un planeta de noche y medir a ojo el desvío.

## Fase 3 — Hecho

- HUD en .NET MAUI para Android: guía a Luna, planetas e ISS; modo "¿qué es eso?" con 155 estrellas y las 88 constelaciones (nombre y figura);
  horizonte, brújula y referencias; aviso de calibración; proyección gnomónica; icono propio.
- Validado en tierra (punto de referencia, 2,6° tras calibrar) y en el cielo (la Luna).
- 253 tests en verde, también en el CI de GitHub en cada PR.

## Fase 4 — Avisos

Plan acordado (2026-10-05): notificaciones locales antes de cada paso visible de la ISS, con la app cerrada; al tocarlas, el HUD con la ISS.
Interruptor AVISOS desactivado por defecto. API nativa de Android (AlarmManager + NotificationCompat), sin paquetes nuevos.
Pasos: 1) reglas y texto en Core; 2) programar y notificar en Android; 3) recálculo diario, tras reiniciar y frente a ColorOS; 4) validación contra Heavens-Above.
Los «eventos destacados» quedan fuera de este primer paso.

### Paso 1 — Reglas y texto de los avisos (2026-10-05)

- Core `Alerts/`: `AlertSettings` (10 min de antelación, silencio 0:00-7:00, recordatorio a las 22:00 de la víspera, TLE de hasta 4 días, mismo paso a ±2 min, horizonte de 3 días),
  `PassAlert` (paso + hora del aviso + si es de víspera), `PassAlertPlanner.Plan` y `PassAlertText` (título y cuerpo en español). Decisión 021.
- Tests unitarios con pasos sintéticos: aviso a 10 min; madrugada → 22:00 de la víspera; límites de las horas de silencio por la hora del aviso (6:59:59 / 7:00, 23:59:59 / 0:00);
  aviso tardío → ya; tardío en silencio → se omite; víspera perdida a las 23:30 → ya; paso empezado; TLE viejo; ya avisado con tolerancia; orden; cambio de hora del 25 oct
  (víspera a las 20:00 UTC el sábado y a las 21:00 UTC el domingo); ajustes inválidos. Texto: el ejemplo del plan literal, salida de la sombra, encendido en el máximo,
  «Mañana», minutos y grados redondeados, hora local tras el cambio de hora, los ocho puntos cardinales.
- Tests de integración con el TLE fijo sobre los pasos de la tabla de Heavens-Above de la Fase 2 (16, 15 y 18 oct).
- Avisos que saldrían del 12 al 22 oct (TLE del 1 oct, Humanes, hora local), coherentes con la tabla de Heavens-Above validada en la Fase 2:

  | Aviso | Texto |
  |---|---|
  | 13/10 07:44 | A las 7:54 pasa la ISS · 11 min · aparece por el SO, máximo 37° al SE |
  | 13/10 22:00 | Mañana a las 7:08 pasa la ISS · 9 min · aparece a 2° al S, máximo 22° al SE |
  | 14/10 22:00 | Mañana a las 6:24 pasa la ISS · 6 min · aparece a 10° al SE, máximo 13° al SE |
  | 15/10 07:47 | A las 7:57 pasa la ISS · 10 min · aparece a 2° al SO, máximo 71° al NO |
  | 16/10 07:02 | A las 7:12 pasa la ISS · 8 min · aparece a 19° al SO, máximo 67° al SE |
  | 16/10 22:00 | Mañana a las 6:28 pasa la ISS · 5 min · aparece a 37° al SE y va bajando |
  | 17/10 22:00 | Mañana a las 5:43 pasa la ISS · 3 min · aparece a 11° al E y va bajando |
  | 18/10 07:06 | A las 7:16 pasa la ISS · 7 min · aparece a 31° al O, máximo 42° al NO |
  | 18/10 22:00 | Mañana a las 6:31 pasa la ISS · 4 min · aparece a 36° al NE y va bajando |
  | 20/10 07:08 | A las 7:18 pasa la ISS · 6 min · aparece a 20° al NO, máximo 21° al N |
  | 20/10 22:00 | Mañana a las 6:33 pasa la ISS · 4 min · aparece a 21° al N y va bajando |

  El aviso del 14 oct a las 7:08 sale de víspera porque cae a las 6:58. Las 7:54 del 13 oct es el paso en el filo del corte de -6° que Heavens-Above también lista.
- Validado con `dotnet test` (298 tests) y build Android sin avisos (la app aún no usa `Alerts/`).

### Paso 2 — Programar y notificar en Android (2026-10-05)

- App `Alerts/`: `PassAlertService` (activar/desactivar, planificar con Core desde la última ubicación y el TLE de la caché, guardar los avisos con su texto en `Preferences`,
  armar la alarma y, al saltar, publicar lo que toca y apuntarlo como avisado), `IAlertPlatform`, `ScheduledAlert` (JSON con generación de código), `LaunchRequests`.
  `Services/ObserverStore`: el HUD guarda la última posición para los cálculos en segundo plano. Decisión 022.
- Android `Platforms/Android/Alerts/`: `AndroidAlertPlatform` (AlarmManager, canal «Pasos de la ISS» de importancia alta, `NotificationCompat`, permisos)
  y `AlertReceiver` (`goAsync`). Icono monocromo `ic_stat_iss`. Manifiesto: `POST_NOTIFICATIONS` y `SCHEDULE_EXACT_ALARM`. Sin paquetes nuevos.
- HUD: botón **AVISOS** en el pie, desactivado por defecto. El primer toque pide el permiso de notificaciones, ofrece abrir «Alarmas y recordatorios»
  y dice cuál es el próximo aviso. Al tocar un aviso se abre el HUD con la ISS seleccionada; si estaba en Diagnóstico vuelve al HUD, y la barra se desplaza hasta el chip de la ISS.
  Se vuelve a planificar al abrir la app, al obtener ubicación y cada vez que salta la alarma.
- Diagnóstico: sección AVISOS (estado, permisos, cuándo se calculó, próximo aviso, los siguientes y la última alarma con hora real frente a objetivo).
  En Debug, «PROBAR AVISO» a 30 s y a 5 min, que pasa por alarmas reales y se marca «[PRUEBA]».
- **Hallazgo en el OPPO**: ColorOS trata las alarmas exactas como inexactas, con una ventana del 75 % del retraso (máximo 1 h), aunque `SCHEDULE_EXACT_ALARM`
  esté concedido. Solución: `Core/Alerts/AlarmSteps`, que arma la alarma por pasos para que el final de la ventana caiga en la hora del aviso. 16 tests. Decisión 023.
- Validado en el OPPO (Android 16):
  - permiso de notificaciones con el diálogo del sistema; «Alarmas y recordatorios» se abre directamente en CieloHud y, al volver, Diagnóstico dice «alarmas exactas sí»;
  - `dumpsys alarm`: alarma `RTC_WAKEUP` de `com.cielohud.app.AlertReceiver`; con los pasos, el final de la ventana coincide con el objetivo;
  - prueba a 5 min **con la app cerrada desde recientes**: saltó a la hora objetivo (+0 s); prueba a 30 s: +0 s. Antes de los pasos, la misma prueba a 30 s llegó a +22,5 s;
  - `dumpsys notification`: canal `iss_passes`, importancia 4, se borra sola al acabar el paso (`timeout`), aparece como banner;
  - al tocarla, el HUD guía a la ISS («ISS, en Orión · acimut 258°, altura 1°») aunque estaba en «¿QUÉ ES?»; la notificación desaparece;
  - del 5 al 8 oct no hay pasos visibles (coincide con la Fase 2), así que todavía no hay un aviso real programado.
  - tocando el aviso con Diagnóstico abierto: vuelve al HUD, la barra se desplaza hasta el chip de la ISS y queda seleccionada.
- Corregido durante el paso: un diálogo mostrado mientras se cierra el del permiso del sistema se cancelaba solo (equivalía a «Ahora no»); ahora espera a que la app vuelva al frente.
- Capturas: `docs/images/avisos-diagnostico.png` y `docs/images/avisos-hud-iss.png`, recortadas sin el pie con la ubicación.
- Pendiente (paso 3): recálculo diario, `BOOT_COMPLETED` y actualización de la app, y probar qué hace ColorOS con «Forzar detención» o la limpieza de recientes.
  Para la prueba se dejó CieloHud en «Permitir actividad en segundo plano» (ajustes de batería de ColorOS); no cambia la ventana de las alarmas.
- `dotnet test` 314 tests; build Android 0 avisos.

### Paso 3 — Avisos con la app cerrada: recálculo diario y eventos del sistema (2026-10-05)

- Core `AlertSettings`: `RefreshInterval` (1 día) y `RetryInterval` (3 h), validados frente al horizonte. Tests.
- App: alarma de recálculo inexacta 24 h después de cada planificación (3 h si falló). `SystemEventsReceiver`: `BOOT_COMPLETED`, `MY_PACKAGE_REPLACED`,
  permiso de alarmas exactas concedido y cambio de zona horaria. Permiso `RECEIVE_BOOT_COMPLETED`. Historial de las últimas planificaciones con su motivo. Decisión 024.
- Diagnóstico: «Calculado» muestra el historial (la más reciente arriba) y «Recálculo», la próxima planificación con la app cerrada. En Debug, «RECÁLCULO (30 s)».
- Validado en el OPPO (Android 16), con CieloHud en «Permitir actividad en segundo plano» (ajustes de batería de ColorOS):

  | Prueba | Resultado |
  |---|---|
  | Instalar la actualización con la app cerrada | `MY_PACKAGE_REPLACED` → «tras actualizar la app»; recálculo armado para el día siguiente |
  | «RECÁLCULO (30 s)» y cerrar la app desde recientes | la alarma arranca el proceso → «recálculo diario» |
  | «Cerrar» en recientes | proceso muerto, app no detenida, alarmas intactas |
  | «Forzar detención» | alarmas borradas y app detenida; al abrirla, todo se vuelve a armar (y Android 15+ le envía `BOOT_COMPLETED`) |
  | `adb reboot` y desbloquear | ColorOS entrega `BOOT_COMPLETED` ~1 min después del desbloqueo → «tras reiniciar»; recálculo armado |

  Comprobado con `logcat` («Alerts planned again after …», «Start proc … for broadcast SystemEventsReceiver»), `dumpsys alarm` y las preferencias de la app (`run-as`).
- Tiempos en Debug: 7 s para planificar tras actualizar; 33 s tras reiniciar, con el móvil recién arrancado (límite de Android: 60 s).
- Sin probar: el cambio de zona horaria (requiere cambiarla en el sistema) y el comportamiento tras reiniciar con la batería en «Modo inteligente», el valor por defecto de ColorOS.
- `dotnet test` 318 tests; build Android 0 avisos.

### Eventos destacados: conjunciones Luna–planeta

Plan acordado (2026-10-05): un aviso cuando la Luna pase cerca de un planeta brillante, con los dos sobre el horizonte y de noche;
al tocarlo, el HUD guía a la Luna. Mismo interruptor AVISOS, mismas horas de silencio y la misma alarma única que la ISS.
Criterio: separación ≤ 5°, los dos a ≥ 10°, Sol ≤ -6°; Venus, Marte, Júpiter y Saturno (Mercurio fuera). Pasos:
5) buscador en Core y validación; 6) cuándo avisar y texto en Core, con un planificador común para la ISS y las conjunciones; 7) integración en la app y prueba en el móvil.
La validación de los avisos de la ISS contra un paso real (paso 4) sigue pendiente, aparte.

### Paso 5 — Buscador de conjunciones (2026-10-05)

- Core `Conjunctions/`: `ConjunctionCriteria` (≤ 5°, ≥ 10°, cómodo a ≥ 20°, Sol ≤ -6°, muestreo 5 min, mismo acercamiento en < 1 día),
  `ConjunctionPoint` (Luna, planeta y separación en un instante), `MoonPlanetConjunction` (planeta; inicio, mejor momento, máxima aproximación y fin de la ventana),
  `IConjunctionFinder` y `ConjunctionFinder`. Decisión 025.
  - Separación **topocéntrica** entre las posiciones aparentes (con refracción), las que ve el ojo: la paralaje de la Luna llega a 1°.
  - **Mejor momento**: mínima separación entre las muestras con los dos a ≥ 20° (si no hay, entre todas), y dentro de la parte de la tarde
    (antes de la medianoche local) si la ventana la tiene.
  - Si el mismo planeta sale de madrugada y al anochecer del mismo día, se queda la de menor separación.
- Consola: `--conjunctions <días>` (1-400) lista ventana, mejor momento y máxima aproximación en hora local. Solo formato.
- Tests unitarios con un cielo falso: ventana y mejor momento, día y crepúsculo civil, separación o altura insuficientes, preferencia por altura cómoda,
  preferencia por la tarde (en hora local, no UTC), ventana solo de madrugada, recorte en `desde` y ventana completa tras `hasta`, madrugada y tarde del mismo acercamiento,
  dos planetas la misma noche, Mercurio fuera por defecto, criterios inválidos.
- **Validación contra JPL Horizons** (Madrid, acimut/altura aparentes con refracción de la Luna y el planeta, Sol sin refracción, cada 5 min por toda la ventana ±30 min;
  la separación se calcula igual a partir de los dos acimut/altura). Las 7 conjunciones del 5 oct 2026 al 2 feb 2027, en hora local:

  | Noche | Planeta | Ventana CieloHud / Horizons | Mejor momento | Separación CieloHud / Horizons | Máx. aproximación (más bajo) | Máx. dif. en la ventana |
  |---|---|---|---|---|---|---|
  | 6 oct | Júpiter | 4:50-7:45 / igual | 7:45, Luna a 45° al E | 1,995° / 1,997° | 7:45 (43°) | 0,0016° |
  | 2 nov | Marte | 4:50-7:15 / igual | 7:15, 66° al S | 4,092° / 4,093° | 7:15 (64°) | 0,0009° |
  | 3 nov | Júpiter | 2:35-7:15 / igual | 3:30, 20° al E | 3,065° / 3,064° | 2:35, 2,60° (10°) | 0,0014° |
  | 7 nov | Venus | 7:15-7:20 / igual | 7:20, 13° al SE | 1,982° / 1,987° | 7:20 (12°) | 0,0044° |
  | 30 nov | Júpiter | 0:45-7:45 / igual | 7:45, 57° al SO | 1,862° / 1,863° | 7:45 (57°) | 0,0016° |
  | 27 dic | Júpiter | 23:20-23:45 / igual | 23:20, 10° al E | 4,747° / 4,746° | 23:20 (10°) | 0,0012° |
  | 23 ene | Júpiter | 20:55-8:00 / igual | 23:00, 33° al E | 1,579° / 1,580° | 23:00 (33°) | 0,0013° |

  Ventanas, mejor momento y máxima aproximación coinciden al minuto aplicando el mismo criterio a los datos de Horizons; la separación, ≤ 0,0044° en todas las muestras.
  Quedan como tests de integración (tolerancia 0,05°).
- Frecuencia con este criterio (sondeo de 12 meses desde Humanes de Madrid, oct 2026-oct 2027): 16 conjunciones en 15 noches, unas 1,3 al mes;
  10 con Júpiter, 5 con Marte, 1 con Venus, ninguna con Saturno (este año la Luna pasa a 6-7° de él). Detalle en la decisión 025.
- **Contraste con Stellarium Web** (2026-10-05, el usuario; ubicación Humanes de Madrid, atmósfera activada, reloj en pausa), 6 oct 7:45:

  | | Stellarium | CieloHud | Diferencia |
  |---|---|---|---|
  | Luna (acimut / altura) | 109,655° / 45,003° | 109,656° / 45,001° | 0,001° / 0,002° |
  | Júpiter (acimut / altura) | 108,722° / 43,130° | 108,720° / 43,124° | 0,002° / 0,006° |
  | Separación | 1,990° | 1,994° | 0,004° |
  | Distancia a la Luna | 369 624 km | 369 645 km | 0,006 % |

  Separación calculada con los acimut y alturas que muestra Stellarium. Parte de la diferencia puede venir de la ubicación (Stellarium no muestra sus coordenadas exactas).
  Pendiente opcional: 3 nov 3:30 (3,07°) y 23 ene 23:00 (1,58°); y verlo en el cielo la madrugada del 6 oct, hacia las 7:30, Luna y Júpiter a 2° al E, a ~45°.
- `dotnet test` 349 tests; build Android 0 avisos (la app aún no usa `Conjunctions/`).

### Paso 6 — Cuándo avisar de una conjunción y con qué texto (2026-10-05)

- **Un solo planificador de horarios** (idea del usuario: las reglas de la ISS y de las conjunciones son las mismas). Core `Alerts/AlertPlanner`
  recibe «candidatos» (hora deseada y hasta cuándo merece la pena) y aplica silencio de 0:00 a 7:00 → 22:00 de la víspera, aviso tardío → ya
  (salvo de madrugada) y orden. `PassAlertPlanner` se queda con lo suyo (TLE de ≤ 4 días, pasos ya avisados a ±2 min) y delega en él;
  sus tests no cambian y siguen pasando. Decisión 026.
- `ConjunctionAlertPlanner`: aviso **30 min antes de que se abra la ventana** (`AlertSettings.ConjunctionLeadTime`); merece la pena hasta que se cierra.
  Las conjunciones con ventanas que se solapan (la Luna junto a dos planetas a la vez) van en **un solo aviso**, la más cercana primero.
  No se repite una ya avisada con el mismo planeta a menos de 1 día (`SameConjunctionTolerance`; `NotifiedConjunction` guarda planeta y mejor momento).
- `ConjunctionAlertText`: «Esta noche, la Luna junto a Júpiter (3°) · mejor hacia las 22:00 al SE». «Esta madrugada» (víspera, mejor momento antes de las 7),
  «Mañana temprano» (víspera, después de las 7), «Ahora» (ventana ya abierta; si ya pasó el mejor momento: «al SO, hasta la 1:00»).
  Hora al cuarto de hora, salvo que el redondeo caiga fuera de una ventana corta; separación en grados enteros («menos de 1°» por debajo de 0,5°);
  dirección de la Luna, que es a donde guiará el HUD. Nombres de los planetas en español en Core.
- Corregido de paso en la ISS: «Mañana a **la** 1:05», no «a las 1:05» (`AlertWords`, compartido por los dos textos).
- Consola: `--conjunctions` lista también los avisos con su hora y texto.
- Tests: planificador común (a su hora, víspera, tardío, tardío de madrugada, caducado, orden); conjunciones (30 min antes, madrugada → víspera, ventana abierta → ya,
  ventana cerrada, ya avisada de madrugada → no se repite al anochecer, otro planeta u otro mes sí, solapes en cadena → un aviso, ventanas separadas → dos);
  texto (ejemplo del plan, redondeo, ventana corta, separaciones, «Esta madrugada», «Mañana temprano», «Esta mañana», «Ahora», dos y tres planetas, cambio de hora);
  integración con las efemérides reales de Madrid.
- Avisos que saldrían planificando el 5 oct (Madrid, hora local), sobre las conjunciones validadas con Horizons en el paso 5:

  | Aviso | Texto |
  |---|---|
  | 5/10 22:00 | Mañana temprano, la Luna junto a Júpiter (2°) · mejor hacia las 7:45 al E |
  | 1/11 22:00 | Mañana temprano, la Luna junto a Marte (4°) · mejor hacia las 7:15 al S |
  | 2/11 22:00 | Esta madrugada, la Luna junto a Júpiter (3°) · mejor hacia las 3:30 al E |
  | 6/11 22:00 | Mañana temprano, la Luna junto a Venus (2°) · mejor hacia las 7:15 al SE |
  | 29/11 22:00 | Mañana temprano, la Luna junto a Júpiter (2°) · mejor hacia las 7:45 al SO |
  | 27/12 22:50 | Esta noche, la Luna junto a Júpiter (5°) · mejor hacia las 23:20 al E |
  | 23/1 20:25 | Esta noche, la Luna junto a Júpiter (2°) · mejor hacia las 23:00 al E |

  Más adelante, el 15 abr 2027: «Esta noche, la Luna junto a Júpiter (4°) y Marte (4°) · mejor hacia las 21:30 al SE».
- `dotnet test` 396 tests; build Android 0 avisos (la app aún no usa los avisos de conjunción).

### Paso 7 — Avisos de conjunción en la app (2026-10-05)

- `PassAlertService` pasa a llamarse **`AlertService`**: planifica las conjunciones de los próximos 3 días y los pasos de la ISS, y guarda los dos tipos
  en **una lista ordenada por hora**. Sigue habiendo una sola alarma de aviso (AlarmSteps sin cambios) más la de recálculo. Decisión 027.
- `ScheduledAlert` gana `Kind` (`Pass` = 0, `Conjunction`) y, para las conjunciones, el planeta y el mejor momento de cada una (`NotifiedConjunction`),
  que se apuntan como avisadas al publicarse y no se repiten. Los avisos guardados por la versión anterior, sin `Kind`, se leen como pasos.
- **Sin órbita de la ISS (sin red), las conjunciones se planifican igual**: Diagnóstico y el resumen de AVISOS lo dicen, y el recálculo se adelanta a 3 h.
  Sin ubicación no se puede planificar nada, como antes.
- Android: canal **«Luna y planetas»** (`moon_planets`, importancia normal: suena, sin banner), icono de luna creciente `ic_stat_moon`,
  la notificación se va sola al cerrarse la ventana, y al tocarla el HUD guía a la **Luna**. Ids de notificación de conjunciones negativos, para no chocar con los de los pasos.
- AVISOS: si no hay nada en 3 días, «ni pasos visibles de la ISS ni la Luna junto a un planeta».
- Diagnóstico (Debug): **«PROBAR CONJUNCIÓN (30 s)»** con la próxima conjunción real (busca hasta 60 días) y su texto de verdad.
- Validado en el OPPO (Android 16), instalando sobre la versión anterior:

  | Prueba | Resultado |
  |---|---|
  | Activar AVISOS | «Próximo aviso: lun 5 22:00 · Mañana temprano, la Luna junto a Júpiter (2°) · mejor hacia las 7:45 al E»: el mismo que da Core (paso 6) |
  | `dumpsys alarm` | `ALERT` pedida a las 21:00 con ventana de 1 h (AlarmSteps: acaba a las 22:00), `exactAllowReason=permission`; recálculo al día siguiente |
  | Preferencias | el aviso guardado con `Kind:1` y `Conjunctions:[{Planet:Jupiter, Best:…}]` |
  | «PROBAR CONJUNCIÓN» con el proceso muerto (`am kill`) | la alarma arranca la app y publica a +3 s del objetivo (arranque en Debug); canal `moon_planets`, importancia 3, caduca al cerrarse la ventana (7:45 del día siguiente) |
  | Tocarla con el HUD en Venus | el HUD pasa a la Luna; la notificación se borra |
  | «PROBAR AVISO» (ISS) | sin cambios: canal `iss_passes`, importancia 4, al tocar → ISS |
  | Canales | «Pasos de la ISS» (4) y «Luna y planetas» (3) |

- ColorOS pinta el icono de la app en la notificación (`oplus_smallicon_use_app_icon`), no el monocromo, igual que con la ISS.
- Capturas: `docs/images/conjuncion-notificacion.png`, `docs/images/conjuncion-diagnostico.png` y `docs/images/conjuncion-hud-luna.png`, recortadas sin el pie con la ubicación.
- Sin probar en el móvil: el camino sin red (habría que borrar la caché del TLE y cortar la red), y el aviso real de esta noche a las 22:00, que queda armado.
- Contrastado con Stellarium Web el 6 oct a las 7:45: separación 1,990° frente a 1,994° (tabla en el paso 5).
- `dotnet test` 396 tests; build Android 0 avisos.

### Paso 8 — Planetas juntos: buscador (2026-10-05)

Plan acordado: avisar también cuando dos planetas se vean juntos. Criterio: ≤ 3°, los dos a ≥ 10°, Sol ≤ -6°; Venus, Marte, Júpiter y Saturno.
Un solo aviso por acercamiento, en su noche más cercana, diciendo qué noches se ven juntos; al tocarlo, el HUD guía al más brillante.
Pasos: 8) buscador en Core y validación; 9) aviso, texto y app.

- Core `Conjunctions/` generalizado: `Conjunction` (antes `MoonPlanetConjunction`) tiene un cuerpo **guía** (la Luna o el planeta más brillante)
  y un **compañero**; `ConjunctionPoint` guarda la posición de los dos. Los avisos con la Luna no cambian: sus tests siguen igual.
- `IConjunctionFinder`: `FindWithMoon` (lo de antes) y `FindPlanetPairs`. Decisión 028.
  - **Una conjunción por acercamiento**: las noches seguidas a ≤ 3° (`ConjunctionCriteria.MaxPlanetSeparationDegrees`) forman un acercamiento;
    se devuelve su noche más cercana, con `Nights` (primera y última noche juntos). Para saber cuál es la más cercana se busca 60 días antes y después
    (`PlanetApproachSearch`). Primero se mira la separación una vez al día y solo se muestrean las noches de los días a menos de 3° + 2°: un año en ~2 s en el PC.
  - **Mejor momento: la mayor altura** del más bajo de los dos (con la misma preferencia por la tarde). Entre dos planetas la separación cambia
    centésimas de grado por hora, así que la regla de la Luna (mínima separación) elegía casi al azar: las 2:30 a 21° en vez de las 7:25 a 63°.
  - **Guía: el más brillante** por orden fijo (Venus, Júpiter, Marte, Saturno).
- Consola: `--conjunctions` (ahora hasta 1100 días) añade la tabla «Planetas juntos». Los avisos de las parejas llegan en el paso 9.
- Tests unitarios con el cielo falso: una conjunción en la noche más cercana y las noches juntos, mejor momento el más alto (tarde o madrugada),
  noche más cercana ya pasada o fuera del rango, búsqueda durante la noche más cercana, dos acercamientos, nunca a 3°, el guía es el más brillante,
  Luna y planetas por separado.
- **Validación contra JPL Horizons** (Madrid, Marte 499 y Júpiter 599, acimut/altura aparentes con refracción; Sol sin refracción):

  | | CieloHud | Horizons |
  |---|---|---|
  | Noche más cercana | 16 nov, 7:25: 1,195° | 16 nov, 7:25: 1,194° |
  | Ventana del 16 nov | 1:35-7:30 | 1:35-7:30 |
  | Mejor momento | 7:25 (el más alto) | 7:25 (el más alto) |
  | Posiciones a las 7:25 | Júpiter 178,912° / 63,177°, Marte 177,938° / 64,291° | 178,909° / 63,178°, 177,937° / 64,292° |
  | Separación noche a noche a las 7:25, del 5 al 27 nov | de 4,194° a 1,195° y vuelta a 4,137° | máx. diferencia 0,0006° |

  Juntos a ≤ 3° del 9 al 23 de noviembre (15 noches), igual que en el sondeo previo. En dos años solo hay otra: Venus–Marte el 8 sep 2028 (2,26°, juntos del 5 al 12).
- `dotnet test` 414 tests; build Android 0 avisos.

### Paso 9 — Avisos de planetas juntos (2026-10-05)

- Core: `ConjunctionAlertPlanner` planifica también las parejas de planetas, **siempre solas** (no se agrupan con las de la Luna aunque coincidan en hora),
  30 min antes de que se abra la ventana de su noche más cercana, o a las 22:00 de la víspera si es de madrugada. Decisión 029.
  No se repiten en todo el acercamiento: misma pareja con la noche más cercana a menos de 30 días (`AlertSettings.SamePlanetPairTolerance`), por si
  al planificar desde otro sitio la noche más cercana cambia. `NotifiedConjunction` guarda también el guía (por defecto la Luna, para leer lo guardado antes).
- `ConjunctionAlertText`: «Marte junto a Júpiter» y «Mañana temprano, Marte junto a Júpiter (1°), lo más cerca en estas semanas · mejor hacia las 7:30 al S · juntos del 9 al 23 nov».
  Meses abreviados propios (sin depender de la cultura del sistema); «del 28 oct al 5 nov» si cambia el mes; sin intervalo si es una sola noche.
- App: `AlertService` planifica las dos clases de conjunción; `ScheduledAlert.Guide` dice a qué objetivo lleva el toque («Luna» o «Júpiter»; lo guardado antes, sin él, a la Luna).
  Mismo canal «Luna y planetas», con la descripción ampliada. Diagnóstico (Debug): «PROBAR LUNA (30 s)» y «PROBAR PLANETAS (30 s)», cada uno con la próxima conjunción real de su clase.
- Consola: `--conjunctions` lista los avisos de las dos clases.
- Tests: planificador (pareja de madrugada → víspera; sola aunque coincida con la Luna; ya avisada → no se repite aunque cambie de noche; la Luna con el mismo planeta no la bloquea),
  texto (el acordado, meses distintos, una sola noche, «Ahora» pasado el mejor momento, nombres de los guías) e integración: en la temporada de Madrid aparece
  «15/11 22:00 · Mañana temprano, Marte junto a Júpiter (1°), lo más cerca en estas semanas · mejor hacia las 7:30 al S · juntos del 9 al 23 nov».
- Validado en el OPPO (Android 16), instalando sobre la versión del paso 7:

  | Prueba | Resultado |
  |---|---|
  | Actualizar con el aviso de esta noche armado | `MY_PACKAGE_REPLACED` → «tras actualizar la app»; el aviso de las 22:00 sigue, ahora con `"Guide":"Luna"`; alarma intacta |
  | «PROBAR PLANETAS» con el proceso muerto | la alarma arranca la app (+2,5 s, arranque en Debug); canal `moon_planets`; el texto calculado en el móvil con su ubicación es el mismo que en Madrid |
  | Tocarla | el HUD pasa a **Júpiter** («Júpiter, en Leo»); la notificación se borra |
  | «PROBAR LUNA» | sin cambios: «La Luna junto a Júpiter», al tocar → Luna |

- Plegada, la notificación corta el texto tras «lo más cerca en estas semanas ·»; al desplegarla se lee entero (`BigTextStyle`).
- Capturas: `docs/images/planetas-notificacion.png` y `docs/images/planetas-hud-jupiter.png`, recortadas sin la ubicación.
- `dotnet test` 426 tests; build Android 0 avisos.

### Paso 10 — Pantalla de próximos eventos (2026-10-05)

- Idea del usuario: ver lo que viene sin esperar al aviso. Botón **EVENTOS** en el pie del HUD, en el sitio de las coordenadas (están en Diagnóstico,
  y así no salen en las capturas). Si no hay ubicación, ahora lo dice el propio HUD: «Sin ubicación · activa el GPS y da permiso». Decisión 030.
- Core `Events/`: `SkyEvent` (clase, cuándo mirar, hasta cuándo, objetivo del HUD, hora, título, detalle y hora del aviso) y `UpcomingEvents`:
  `Build` junta pasos visibles y conjunciones y les pone la hora de su aviso con **los mismos planificadores** que los avisos (así la lista y las
  notificaciones no se contradicen); `DayLabel` («Hoy», «Mañana», «lun 2 nov») y `AlertLabel` («Aviso el dom 1 nov a las 22:00»). Textos absolutos,
  no relativos al aviso: «≈7:45 · La Luna junto a Júpiter (2°) · al E»; para la ISS, «21:43 · Pasa la ISS · 5 min · aparece por el NO, máximo 67° al SE».
- **Muestreo de conjunciones en pasos fijos** (:00, :05… UTC): antes empezaba en el instante del cálculo y la misma conjunción salía a las 7:44 o a las 7:45
  según cuándo se calculara. Ahora el resultado no depende de eso. Test nuevo.
- App: `AlertService.UpcomingAsync` calcula al abrir la pantalla, también con los avisos apagados: conjunciones a **30 días** y pasos de la ISS a **3**
  (más allá el TLE no es fiable). `EventsPage`, agrupada por día, con la paleta del modo nocturno; al tocar un evento el HUD guía a su objetivo.
  La línea del aviso solo aparece con AVISOS activado.
- Tests: paso, conjunción con la Luna y pareja de planetas (hora, título, detalle, objetivo y aviso), orden, ya avisados sin aviso, paso en curso sí y acabado no,
  sin órbita de la ISS, etiquetas de día (también con el cambio de hora) y de aviso.
- Validado en el OPPO: lista de hoy («Mañana ≈7:45 · La Luna junto a Júpiter (2°) · al E · Aviso hoy a las 22:00», y el 2 y 3 nov la Luna con Marte y con Júpiter;
  sin pasos de la ISS en 3 días, como en la Fase 2); tocar un evento con el HUD en Venus vuelve al HUD en la Luna; modo nocturno en rojo sin blancos.
  Captura: `docs/images/eventos.png`.
- `dotnet test` 442 tests; build Android 0 avisos.

## Mejoras fuera de fase

Priorizadas por el usuario aunque la fase actual sea la 4.

### Modo nocturno rojo (2026-10-05)

- Por qué: de noche, los blancos, verdes y cianes del HUD rompen la adaptación del ojo a la oscuridad (unos 20 minutos en conseguirse).
- `Hud/HudPalette`: todos los colores de la app por función (fondo, cuatro niveles de texto, fijado, guía, marcador, alerta, estrella…), con `Normal`
  (la estética de siempre) y `Night` (negro puro y cinco niveles de rojo). Ya no hay colores sueltos en `HudDrawable`, los chips ni los XAML. Decisión 019.
- `Hud/NightMode`: se recuerda en `Preferences`, publica la paleta como recursos de la app (`DynamicResource` en `HudPage` y `DiagnosticsPage`)
  y en Android (`Platforms/Android/NightWindow`) oculta las barras del sistema y baja el brillo **de la ventana** al mínimo (0,01), sin tocar el ajuste del sistema.
  Se re-aplica cada vez que la app vuelve al frente.
- Botón **NOCHE** en el pie del HUD, junto a DIAGNÓSTICO; relleno cuando está activo.
- En modo nocturno: retícula, marcador, flecha, estrellas, figuras, horizonte, brújula, textos, paneles, chips, pie, botones y la página de diagnóstico en rojo;
  los estados se distinguen por intensidad y grosor (fijado: rojo más vivo, trazo de 3 px y pulso; aviso de calibración: el rojo más vivo parpadeando).
- Validado en el OPPO (Android 16), con capturas:
  - modo normal idéntico al de antes del cambio;
  - al pulsar NOCHE cambian en el acto HUD, chips, pie y diagnóstico; sin blancos ni azules;
  - barras del sistema ocultas; `dumpsys display` muestra `mWindowManagerBrightnessOverride=0.01` con la app delante y `NaN` en la pantalla de inicio,
    y de nuevo 0,01 al volver;
  - cerrando la app a la fuerza y abriéndola de nuevo arranca en modo nocturno;
  - al desactivarlo vuelven las barras, los colores normales y el brillo del sistema.
  - `dotnet test` 253 tests; build Android 0 avisos.
- Pendiente: probarlo de noche de verdad (si los rojos más tenues, como figuras y estrellas débiles, se ven con el brillo al mínimo, y si el brillo es cómodo).
  Sin comprobar en pantalla: el estado "en objetivo" y el aviso de calibración en rojo (no se pueden forzar desde adb); usan los mismos colores de rol que se ven en las capturas.
- Sin comprobar: el efecto de pulsación de los botones de Android (ripple), que no usa la paleta y podría verse claro un instante.

### Modo nocturno legible a oscuras (2026-10-05)

- Problema (el usuario, en una habitación a oscuras): al pulsar NOCHE el rojo se veía un momento y se fundía a negro; no se leían los botones.
- Medido en el OPPO con `dumpsys display` (escala del panel 0-2047): el brillo automático a oscuras es **12**; el 1 % forzado daba ~20, casi lo mismo.
  Lo que fallaba era la paleta: un rojo puro da ~1/5 de la luz del blanco, y los botones usaban rojos al 63 % y 31 %. El «fundido» es Android bajando
  el brillo hasta el forzado en ~1,2 s (977 → 204 medido con la habitación iluminada).
- Cambios (decisión 031):
  - Paleta nocturna con **rojos casi plenos** para textos y botones (`#FF3020`) y medios para lo decorativo; sigue sin verde ni azul.
  - Brillo de la ventana en modo noche: **10 % por defecto** (204 de 2047; el usuario lee bien así), **ajustable**: con el modo noche activado,
    NOCHE abre un panel propio en rojo con 3 % · 6 % · 10 % · 20 % y «Salir del modo noche». Se guarda en `Preferences`.
  - El HUD **mantiene la pantalla encendida** mientras está delante (`KeepScreenOn`): se sostiene sin tocar y el móvil la apagaba a los 30 s.
- Probado en el OPPO: con 10 % el usuario lee el HUD a oscuras; brillo medido 204 estable tras la transición; captura con el HUD en rojo pintado.
  Pendiente: probar el panel de brillo en el móvil (la instalación quedó a medias por desconexión; instalado después a las 18:06).

### Primera versión descargable: 0.4.0 (2026-10-06)

- Para que cualquiera pueda instalarla sin el SDK: APK firmado publicado a mano en GitHub Releases como *pre-release* (la Fase 4 sigue abierta). Decisión 032.
- **Firma**: clave propia (`CN=CieloHud`, RSA 4096, ~27 años) generada fuera del repo, en `%USERPROFILE%\.cielohud\` (almacén y fichero de contraseña;
  el usuario guarda una copia: sin ella no se puede actualizar lo instalado). `scripts/publish-apk.ps1` compila en Release desde limpio, firma y **verifica**
  la firma con `apksigner` antes de dar el APK por bueno.
  - Fallo encontrado: `apksigner` lee las contraseñas `file:` línea a línea (almacén y luego clave) del mismo fichero; con una sola línea fallaba la firma
    y una segunda compilación, al darlo por actualizado, dejó un APK **sin firmar** con nombre de firmado. Ahora la contraseña va por variable de entorno,
    se compila desde limpio y se verifica.
- **Versión** 0.4.0, `ApplicationVersion` 400 (mayor×10000 + menor×100 + parche).
- **Acerca de** (botón ACERCA DE en el pie, en lugar de DIAGNÓSTICO): qué es, privacidad, enlace al repositorio, datos y licencias, y el acceso a Diagnóstico.
  Los textos de `LICENSE`, `THIRD-PARTY-NOTICES.md` y `licenses/*` van dentro del APK (BSD y OFL piden acompañar al binario) y se muestran ahí.
- README: sección «Instalar en tu móvil» (origen desconocido, Play Protect, permisos, batería); fila del modo nocturno y número de tests al día.
