# Estado

Fase actual: 5 (ver PLAN.md). Fases 1 y 2 completadas el 2026-10-02; fase 3 el 2026-10-05; fase 4 el 2026-10-06.

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
La validación de los avisos de la ISS contra Heavens-Above (paso 4) se hizo al final, el 2026-10-06.

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
- Sin probar en el móvil: el aviso real de esta noche a las 22:00, que queda armado. El camino sin red se probó en el paso 13 de Mercurio.
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

### Mercurio: los días buenos para verlo

Encargo: decidir con datos si CieloHud debe avisar de los días buenos para ver Mercurio, que estaba fuera de las conjunciones (decisión 025)
y en VISION «como extra». Sondeo previo de dos años (oct 2026-sep 2028, Humanes de Madrid, cada minuto del crepúsculo): con el criterio de siempre
(≥ 10° con el Sol ≤ -6°) y magnitud ≤ 0,5 salen **3 temporadas al año de 5 a 15 días**, con Mercurio a 10-14° y de magnitud -0,6 a 0,1 en su mejor día.
**La elongación no basta**: la de octubre de 2026 (25°) deja a Mercurio a 3,4° con el Sol a -6°; la buena es la de noviembre al amanecer (19,5°). Decisión 033.
Plan acordado: 11) buscador de temporadas en Core y validación; 12) aviso, texto y EVENTOS en Core; 13) la app. Sin APKs nuevos por ahora.

### Paso 11 — Mercurio: buscador de temporadas (2026-10-06)

- Core `Apparitions/`: `MercuryCriteria` (≥ 10°, Sol ≤ -6°, magnitud ≤ 0,5; muestreo cada minuto con una pasada previa cada 5 min; 30 días de margen),
  `MercuryWindow` (anochecer o amanecer; inicio, mejor momento y fin de la ventana del día; magnitud), `MercuryApparition` (temporada: mejor día,
  primer y último día, número de días), `IMercuryApparitionFinder` y `MercuryApparitionFinder`. Decisión 033.
  - **Ventana**: los minutos con el Sol ≤ -6° y Mercurio a ≥ 10°; **mejor momento**, el más alto (en la práctica, con el Sol llegando a -6°: el inicio
    al anochecer, el final al amanecer). Se queda si la magnitud en el mejor momento es ≤ 0,5. Anochecer o amanecer según la hora local.
  - **Temporada**: días locales seguidos con ventana en la misma franja; **mejor día**, el de Mercurio más alto. `Find` devuelve las temporadas cuyo
    mejor día cae en el rango, enteras (como las parejas de planetas); `FindWindows`, cada día.
  - Una pasada cada 5 min busca dónde el Sol y Mercurio están a menos de 2° de los límites; solo ahí se muestrea cada minuto (las ventanas duran 1-21 min).
    Pasos fijos en UTC (`Sky/TimeGrid`, ahora compartido con las conjunciones). Medido en el PC: 30 días (lo que pedirá EVENTOS, más el margen) en 0,18 s; un año en 0,5 s.
- `IMagnitudeService` y `AstronomyEngineMagnitudeService` (`Illumination` de Astronomy Engine), aparte de `ISolarSystemService` para no tocar los cielos falsos de los tests.
- Consola: `--mercury <días>` (1-1100) lista las temporadas y cada día. Solo formato.
- Tests unitarios con un cielo falso: ventana de anochecer y de amanecer, alto solo con el cielo claro, ventana corta entre dos muestras gruesas,
  tope de magnitud (0,5 sí, 0,51 no) y magnitud en el mejor momento, días seguidos → una temporada con su mejor día, un día sin ventana o demasiado débil
  la parte en dos, anochecer y amanecer por separado, temporada entera con solo el mejor día en el rango, mejor día fuera del rango, durante la ventana del mejor día,
  ventana abierta al final del rango, resultado independiente del instante del cálculo, franja según la hora local, criterios inválidos.
- **Validación contra JPL Horizons** (Madrid, Mercurio 199 con refracción y magnitud aparente, Sol sin refracción, cada minuto; el mismo criterio aplicado a sus datos):

  | Temporada | Días CieloHud / Horizons | Mejor día | Ventanas | Altura en el mejor momento |
  |---|---|---|---|---|
  | Amanecer, nov 2026 | 14-28 nov (15) / igual | 20 nov, 7:37, 12,39° al SE (118°), mag. -0,6 | las 15 iguales al minuto | máx. dif. 0,001° |
  | Anochecer, feb 2027 | 31 ene-7 feb (8) / igual | 4 feb, 19:05, 11,08° al SO (247°), mag. -0,5 | las 8 iguales al minuto | máx. dif. 0,001° |
  | Anochecer, may 2027 | 15-28 may (14) / 15-27 may (13) | 26 may, 22:06, 13,39° al O (292°), mag. 0,2 | las 13 comunes iguales al minuto | máx. dif. 0,001° |
  | Anochecer, oct 2026 | ninguna / ninguna | 12 oct: Mercurio a 3,39° con el Sol a -6° (Horizons 3,391°) | — | — |

  En todos los minutos del crepúsculo (Sol entre 0° y -12°, Mercurio sobre el horizonte) de las cuatro descargas: altura ≤ 0,0015°, acimut ≤ 0,0012°, Sol ≤ 0,0004°.
  La **magnitud** difiere hasta 0,13 (Astronomy Engine más brillante; Horizons usa un modelo más reciente), y eso cambia el último día de mayo de 2027:
  0,44 aquí y 0,51 en Horizons, frente al tope de 0,5. Aceptado: el tope es una regla práctica, no un umbral físico (decisión 033).
  Quedan como tests de integración (altura 0,05°, magnitud 0,15): los 15 días de noviembre, las tres temporadas del año y octubre sin ninguna.
- Sondeo y comparación en el scratchpad, fuera del repo.
- `dotnet test` 482 tests; build Android 0 avisos (la app aún no usa `Apparitions/`).

### Paso 12 — Aviso de Mercurio, texto y EVENTOS (2026-10-06)

- Elegido por el usuario: **un aviso por temporada, en su mejor día**, como las parejas de planetas. Decisión 034.
- Core `Alerts/`: `MercuryAlertPlanner` (sobre `AlertPlanner`): aviso **30 min antes de que se abra la ventana del mejor día**
  (`AlertSettings.MercuryLeadTime`); al amanecer cae en las horas de silencio y pasa a las 22:00 de la víspera. Merece la pena hasta que se cierra la ventana.
  No se repite: misma franja (anochecer o amanecer) con el mejor momento a menos de 30 días (`SameMercurySeasonTolerance`; `NotifiedMercury` guarda franja y mejor momento).
- `MercuryAlertText`: «Mercurio al amanecer» · «Mañana al amanecer, Mercurio a 12°, lo más alto en estas semanas · mejor hacia las 7:35 al SE · se ve del 14 al 28 nov».
  «Hoy / Mañana / El 4 feb al anochecer»; «Ahora…» si se planifica con la ventana abierta, y pasado el mejor momento «Ahora, Mercurio bajo al O, hasta las 19:11».
  - La altura va en el texto porque está bajo: hace falta un horizonte despejado.
  - **Hora a 5 min**, la marca más cercana **dentro de la ventana**: al anochecer el mejor momento es el inicio, y «22:05» para una ventana que abre a las 22:06
    sería antes de que oscurezca; dice «22:10». Sin marca dentro (18:47-18:49), el minuto exacto.
  - El intervalo de días sale de `AlertWords.DateRange`, ahora común con «juntos del 9 al 23 nov».
- EVENTOS: `SkyEventKind.Mercury` y `UpcomingEvents.Build` con las temporadas y las ya avisadas (parámetros opcionales al final: la app sigue igual hasta el paso 13).
  «≈7:35 · Mercurio al amanecer (12°) · al SE · se ve del 14 al 28 nov», objetivo «Mercurio», con la hora de su aviso por el mismo planificador.
- Consola: `--mercury` lista también los avisos. Los de los próximos dos años desde Madrid:

  | Aviso | Texto |
  |---|---|
  | 19/11/2026 22:00 | Mañana al amanecer, Mercurio a 12°, lo más alto en estas semanas · mejor hacia las 7:35 al SE · se ve del 14 al 28 nov |
  | 4/2/2027 18:35 | Hoy al anochecer, Mercurio a 11°, lo más alto en estas semanas · mejor hacia las 19:05 al SO · se ve del 31 ene al 7 feb |
  | 26/5/2027 21:36 | Hoy al anochecer, Mercurio a 13°, lo más alto en estas semanas · mejor hacia las 22:10 al O · se ve del 15 al 28 may |
  | 3/11/2027 22:00 | Mañana al amanecer, Mercurio a 12°, … · mejor hacia las 7:15 al E · se ve del 30 oct al 11 nov |
  | 19/1/2028 18:17 | Hoy al anochecer, Mercurio a 10°, … · mejor hacia las 18:47 al SO · se ve del 17 al 21 ene |
  | 8/5/2028 21:19 | Hoy al anochecer, Mercurio a 14°, … · mejor hacia las 21:50 al O · se ve del 28 abr al 10 may |

  Horas y alturas sobre las temporadas validadas con Horizons en el paso 11.
- Tests: planificador (anochecer a 30 min, amanecer → víspera, con la ventana abierta → ya, ventana cerrada, ya avisada aunque cambie el mejor día,
  la otra franja o una temporada de hace meses no la bloquean, orden), texto (amanecer de víspera, anochecer del día, fecha, «Ahora» antes y después del mejor
  momento, un solo día, redondeo dentro de la ventana en seis casos), EVENTOS (campos, ya avisada sin aviso, desaparece al cerrarse la ventana, orden con
  las conjunciones) e integración con las efemérides reales (los tres avisos del año, antes de su ventana y fuera de las horas de silencio).
- `dotnet test` 509 tests; build Android 0 avisos (la app aún no usa los avisos de Mercurio).

### Paso 13 — Mercurio en la app (2026-10-06)

- `AlertService` planifica también las temporadas de Mercurio cuyo mejor día cae en los próximos 3 días (sin red también: solo necesita las efemérides),
  en la misma lista y con la misma alarma única. `ScheduledAlert` gana `Kind = Mercury` (2) y `Mercury` (`NotifiedMercury`), que al publicarse se guarda
  en `alerts_notified_mercury` y se poda a los 30 días. Al tocarlo, el HUD guía a **Mercurio**. Mismo canal, «Luna y planetas», con la descripción ampliada.
- EVENTOS calcula las temporadas a 30 días con las ya avisadas (`UpcomingEvents.Build`, paso 12). El resumen de AVISOS sin nada que avisar lo menciona.
- Diagnóstico (Debug): **«PROBAR MERCURIO (30 s)»** con la próxima temporada real (busca hasta 200 días: las de anochecer pueden estar a seis meses) y su texto de verdad.
- `MauiProgram`: `IMagnitudeService` e `IMercuryApparitionFinder`.
- **Fallo encontrado y corregido: al tocar un aviso, a veces el HUD no cambiaba de objetivo** (también con la Luna o Júpiter; no era de Mercurio).
  Al salir con «Atrás» la actividad se cierra pero el proceso sigue, y al volver se crea otra con un HUD nuevo. El HUD viejo seguía suscrito al evento
  estático `LaunchRequests.Requested` y se quedaba la petición antes que el visible. Ahora (decisión 035): un solo oyente, el último HUD creado;
  una actividad nueva (`OnCreate`) solo guarda la petición, que el HUD nuevo recoge al aparecer, y solo `OnNewIntent` avisa al HUD abierto.
  Además, en una actividad nueva la barra de objetivos aún no está maquetada: el desplazamiento hasta el chip espera a que tenga tamaño.
- Validado en el OPPO (Android 16), build de Debug:

  | Prueba | Resultado |
  |---|---|
  | «PROBAR MERCURIO», salir con «Atrás», tocar | notificación en `moon_planets`, a la hora; al tocar se crea una actividad nueva y el HUD guía a Mercurio (antes de la corrección, se quedaba en la Luna) |
  | El mismo `Intent` por `adb` tras «Atrás» / con la app abierta / con el proceso muerto (`am kill`) | Mercurio / Saturno / Júpiter e ISS, con su chip resaltado y a la vista |
  | Preferencias | la prueba no se apunta como avisada; nada pendiente en 3 días (el mejor día de noviembre es el 20) |
  | Tocar «La Luna junto a Marte» en EVENTOS | el HUD pasa a la Luna, con su chip resaltado (camino `Request` con la actividad viva) |

- **Camino sin red** (pendiente desde el paso 7), con la caché del TLE apartada (`run-as mv`) y wifi y datos cortados (`svc wifi/data disable`); la app, al fondo y de nuevo delante:

  | Prueba | Resultado |
  |---|---|
  | Planificación «al abrir la app» | `alerts_problem` = «sin órbita de la ISS (sin red): solo avisos de la Luna y los planetas»; sin errores en logcat |
  | Recálculo | adelantado a 3 h: `REFRESH_ALERTS` a las 19:16 (`dumpsys alarm`), en vez de las 16:15 del día siguiente |
  | EVENTOS | la Luna con Marte (2 nov) y con Júpiter (3 nov), con la hora de su aviso, y «Ahora mismo: sin órbita de la ISS (sin red): solo la Luna y los planetas» |
  | Red y caché devueltas, app de nuevo delante | el problema desaparece y el recálculo vuelve a 24 h |

  No había avisos pendientes en 3 días (ni conjunciones ni Mercurio), así que no se pudo ver uno programado sin red; los planificadores son los mismos que con red.

- Sin probar en el móvil: Mercurio en EVENTOS (la lista mira 30 días; aparecerá desde el 21 oct) y el aviso real (víspera del 20 nov, a las 22:00).
- `dotnet test` 509 tests; build Android 0 avisos.

### Paso 4 — Pasos de la ISS contra Heavens-Above (2026-10-06)

- Plan acordado al principio de la fase: cerrarla tras ver un paso real con su aviso. El usuario prefirió cerrarla ya con la comparación
  contra Heavens-Above y dejar la observación como comprobación en el cielo pendiente (abajo).
- Heavens-Above, Humanes de Madrid (las mismas coordenadas del pueblo que la consola), búsqueda del 6 oct 0:00 al 16 oct 0:00, órbita con época del 6 oct.
  CieloHud con el TLE de CelesTrak de la misma época (6 oct 00:20 UTC). Heavens-Above da el inicio y el fin a 10° de altura; para compararlos,
  se buscó en CieloHud cuándo cruza la ISS los 10° (cada segundo, `Sgp4SatelliteService`).

  | Paso | Heavens-Above: 10° · máximo · 10° | CieloHud: 10° · máximo · 10° |
  |---|---|---|
  | 11 oct | 7:56:35 SSE · 7:58:05, 12° SE · 7:59:35 ESE | 7:56:37 (155°) · 7:58:05, 12,4° (129°) · 7:59:34 (103°) |
  | 13 oct | 7:56:53 SSO · 8:00:02, 37° SE · 8:03:12 ENE | 7:56:54 (205°) · 8:00:02, 36,5° (136°) · 8:03:11 (68°) |
  | 14 oct | 7:10:13 S · 7:12:52, 21° SE · 7:15:33 E | 7:10:14 (184°) · 7:12:52, 21,5° (133°) · 7:15:32 (81°) |
  | 15 oct | 6:24:12 SSE · 6:25:45, 13° SE · 6:27:19 E | 6:24:13 (156°) · 6:25:46, 12,7° (129°) · 6:27:19 (101°) |
  | 15 oct | 7:58:47 OSO · 8:02:10, 71° NO · 8:05:34 NE | 7:58:48 (239°) · 8:02:10, 71,2° (325°) · 8:05:34 (50°) |

  Los cinco pasos que Heavens-Above marca como «visible» en esos días son los cinco de CieloHud (`--passes`), a ≤ 2 s y con la misma altura máxima;
  los de día («luz día») no salen en ninguno. El 16 oct 7:14 (67°) que da CieloHud queda fuera del rango de búsqueda de Heavens-Above.
- Curiosidad de Heavens-Above: su lista «sólo los visibles» omite el 11 oct y el 15 oct de las 7:58, que en la vista «todos» marca como visibles.
  Los dos son de madrugada con el Sol a -5,6° / -6,0° al pasar la ISS de 10°, como el del 13 (-5,9°), que sí lista; parece un filtro propio de esa lista.
- Los avisos que da Core para estos pasos son los de la tabla del paso 1 (13/10 07:44 «A las 7:54 pasa la ISS · 11 min · aparece por el SO, máximo 37° al SE», etc.).
- **Pendiente en el cielo**, sin bloquear la fase: ver el 13 oct que el aviso de las 7:44 llega y que la ISS pasa por donde dice (si está nublado, el 16 oct a las 7:12, 67°).
  Hacia el 10 oct, comprobar en Diagnóstico y con `dumpsys alarm` que está programado (la app del móvil es ahora una build de Debug con AVISOS activado).

## Fase 4 — Hecho

- **Avisos con la app cerrada** (AlarmManager con una sola alarma y recálculo diario, también tras reiniciar o actualizar, y frente a los retrasos de ColorOS):
  pasos visibles de la ISS (10 min antes; de madrugada, la víspera a las 22:00), la Luna junto a un planeta, dos planetas juntos y los mejores días de Mercurio.
  Canales «Pasos de la ISS» y «Luna y planetas»; al tocar un aviso, el HUD guía a su objetivo. Sin red, la Luna, los planetas y Mercurio se siguen avisando.
- **EVENTOS**: lo que viene en 30 días (3 para la ISS), con la hora de su aviso calculada por los mismos planificadores.
- Validado: pasos de la ISS contra Heavens-Above (5 de 5 a ≤ 2 s); conjunciones (separación ≤ 0,0044°) y Mercurio (ventanas al minuto, posiciones ≤ 0,0015°)
  contra JPL Horizons; una conjunción contra Stellarium Web; avisos, toques y el camino sin red en el OPPO.
- 509 tests en verde, también en el CI. Decisiones 021-035.
- Fuera de la fase, a petición del usuario: modo nocturno y APK descargable (0.4.0).

## Fase 5 — Fichas del objeto

Plan acordado (2026-10-06): los datos del momento se calculan en Core con tests contra Horizons/SIMBAD, y los textos van aparte, revisados uno a uno (decisión 020).
Pasos: 1) Luna y planetas: datos; 2) formato de los textos y los 7 objetivos; 3) la ficha en la app para los 7 objetivos; 4) lunas galileanas;
5) anillos de Saturno; 6) altura y velocidad de la ISS; 7) estrellas: paralaje en el catálogo, años luz y ficha en «¿qué es?»; 8) textos de las estrellas por lotes;
9) constelaciones: ficha en «¿qué es?» y textos por lotes.
Ampliado el 2026-10-07 (el usuario eligió dibujos calculados frente a fotos, tras ver un boceto): un paso de **dibujos** después del 5, y los siguientes
se corren: 6) dibujos de la Luna y los planetas con su fase, Júpiter con sus lunas y Saturno con sus anillos; 7) ISS; 8) estrellas: datos; 9) textos de las
estrellas; 10) constelaciones.
Elegido por el usuario: al llegar a AQUÍ la ficha se abre sola tras ~1,5 s, una vez por objetivo; en «¿qué es?», con un botón «VER FICHA». Corregido el
2026-10-07 tras usarla (decisión 045): en la guía, VER FICHA siempre visible, resaltado en AQUÍ; la ficha ya no se abre sola.
Se cierra con «CERRAR», con Atrás o al cambiar de objetivo; en modo noche, panel propio en rojo. Estrellas con paralaje dudosa: «unos…» con 2 cifras,
e intervalo si el error relativo pasa del 20 %. Los textos, un Markdown por objeto con sus fuentes; si falta, la ficha sale solo con datos.
Hallazgo al revisar: el catálogo `BrightStars` no guarda la paralaje; se añadió en el paso 8, de Hipparcos y no de SIMBAD (decisión 044).

### Paso 1 — Luna y planetas: datos del momento (2026-10-06)

- Core `Cards/`: `ISolarSystemFactsService` y `AstronomyEngineSolarSystemFactsService`. Decisión 036.
  - `MoonFacts`: distancia y tiempo de luz desde el observador, **fracción iluminada topocéntrica**, fase (diferencia de longitud Luna–Sol geocéntrica),
    nombre de la fase y la próxima fase principal. `Illumination` de Astronomy Engine es geocéntrica, y desde Madrid la paralaje de la Luna mueve
    la fracción hasta medio punto (21,49 % frente a 21,04 % el 6 oct a las 7:45), así que se calcula con los vectores Sol–Luna–observador.
  - `MoonPhases.Name`: la fase principal se nombra hasta **1 día** antes o después de su instante; entre ellas, creciente o gibosa, creciente o menguante.
  - `PlanetFacts`: distancia y tiempo de luz (`LightTravel`, velocidad de la luz exacta).
  - `FactsText`: «Gibosa creciente, iluminada al 71 %», «Luna llena el lun 26 oct a las 5:12», «A 369 600 km: su luz tarda 1,2 segundos en llegar»,
    «A 876 millones de km», «Esta luz salió de Júpiter hace 49 minutos»; menos de 10 min, «2 min y 40 s»; más de una hora, «1 h y 10 min».
    Espacio de no separación entre miles y antes de unidades y «%».
- Consola: `--card <moon|mercury|venus|mars|jupiter|saturn>` muestra las frases de la ficha y los valores en bruto para contrastarlos. Solo formato.
- **Validación contra JPL Horizons** (Madrid, consultas del 6 oct):

  | Dato | CieloHud | Horizons | Diferencia |
  |---|---|---|---|
  | Luna iluminada, 6 oct 5:45 UTC (topocéntrica) | 21,036 % | 21,035 % | 0,001 puntos |
  | ídem 12 oct 19:00 / 20 oct 21:00 | 4,791 % / 70,433 % | 4,792 % / 70,430 % | ≤ 0,003 puntos |
  | Luna nueva / cuarto creciente / llena / cuarto menguante (oct-nov 2026) | 15:50:36 / 16:13:19 / 04:12:15 / 20:28:58 UTC | 15:50:05 / 16:12:41 / 04:11:48 / 20:28:27 | 27-38 s |
  | Distancia y tiempo de luz de los cinco planetas, 6 oct 5:45 UTC | Júpiter 48,7227 min | 48,7180 min | ≤ 0,01 % en todos |

  Los instantes de las fases según Horizons salen de interpolar la diferencia de longitud eclíptica Luna–Sol (cantidad 31) diez minutos antes y después;
  la hora se muestra al minuto. Quedan como tests de integración (iluminación 0,05 puntos, distancia y tiempo de luz 0,1 %, fases 1 min).
- Tests unitarios: nombre de la fase (en el instante, hasta un día antes y después, justo fuera, los cuatro tramos, ángulos fuera de rango) y frases
  (porcentajes y redondeo, día y hora local de la próxima fase con el cambio de hora y «la 1:05», distancias, tiempos de luz de 1 s a 2 h).
- `dotnet test` 573 tests; build Android 0 avisos (la app aún no usa `Cards/`).

### Paso 2 — Formato de los textos y los 7 objetivos (2026-10-06)

- **Formato** (decisión 037): un Markdown por objeto en `src/CieloHud.Core/Cards/Texts/es/{targets,stars,constellations}/<id>.md`,
  incrustado en Core: `# Título`, el texto (uno o más párrafos) y `## Fuentes` con una línea por fuente. Los identificadores son el nombre del cuerpo
  (`Moon.md`, `Jupiter.md`), `ISS.md`, la designación de Bayer de las estrellas (`alf-CMa.md`) y el símbolo IAU de las constelaciones (`Ori.md`).
- Core `CardTexts`: `Find(CardKey)`, `All` y `Parse`. Los espacios dentro de un número («282 000») y entre número y unidad («430 °C», «109 m», «27 %»)
  pasan a ser de no separación. Un objeto sin fichero no tiene texto todavía, y su ficha saldrá solo con datos.
- **Reglas comprobadas por los tests**: como máximo 400 caracteres (lo que cabe bajo el HUD sin desplazarse), al menos una fuente, texto plano
  (sin marcas de Markdown), nada que dependa del momento («hoy», «ahora», «esta noche»…: eso lo pone el cálculo), solo objetos que existen
  (cuerpo, estrella del catálogo o constelación IAU), los 7 objetivos con el nombre que usa la app, y la cuenta de lo pendiente (155 estrellas y 88 constelaciones).
- **Los 7 textos**: Luna, Mercurio, Venus, Marte, Júpiter, Saturno e ISS, de 280 a 389 caracteres. Borradores escritos con ayuda de IA fuera del repo,
  con cada cifra fija sacada de su fuente (fichas y páginas de datos de la NASA, la RAE, decisiones 010, 011 y 033). Revisados por el usuario en la conversación
  y, uno a uno, en la PR (una casilla por texto).
  Cambios de la revisión: en Mercurio, «mucha gente no lo ha visto nunca» (sin fuente) pasa a «unas pocas semanas al año» (sondeo de la decisión 033);
  en la ISS se quita «donde suelen vivir siete personas» (cambia con las tripulaciones); la cita de la RAE para «lucero» la comprobó el usuario
  (dle.rae.es no deja leerse con herramientas automáticas).
- Pendiente: no se dice que las lunas de Júpiter se vean con prismáticos hasta tener su magnitud calculada (paso 4).
- Consola: `--card` muestra también el texto, ajustado a 100 columnas, o «(sin texto todavía)».
- `dotnet test` 594 tests; build Android 0 avisos (la app aún no usa los textos).

### Paso 3 — La ficha en la app para los 7 objetivos (2026-10-07)

- **Cuándo se abre** (decisión 038): en modo guía, sola tras **1,5 s seguidos en AQUÍ**, una vez por objetivo (`Core/Cards/CardAutoOpen`);
  en «¿QUÉ ES?», con el botón **VER FICHA** bajo el nombre de lo reconocido, que se mantiene 2 s aunque la retícula se salga un momento
  (`Core/Cards/RecentMatch`). Solo los 7 objetivos; estrellas y constelaciones, en los pasos 7 y 9.
- **Cómo se cierra**: CERRAR, el botón Atrás o elegir otro objetivo (también al tocar un aviso). Al bajar el móvil se sale de AQUÍ, pero la ficha sigue
  abierta. El panel de brillo del modo noche la sustituye si se abre.
- **Qué muestra** (`Hud/CardBuilder`): nombre, «planeta · en Cáncer», el texto revisado y «AHORA» con los datos del paso 1, que se recalculan cada 10 s
  mientras está abierta. La ISS aún no tiene datos (paso 6). Panel propio con la paleta, en rojo en modo noche, con la altura de su contenido
  (como mucho 440 dp; si no cabe, se desplaza).
- Tests de Core: abrir tras el tiempo, una sola vez aunque se salga y se vuelva, salir antes reinicia la cuenta, objetivo nuevo, nunca en AQUÍ;
  mantener lo reconocido durante el margen, reiniciarlo al verlo de nuevo, otro objeto lo sustituye, olvidarlo.
- Validado en el OPPO (Android 16, Debug, de día):

  | Prueba | Resultado |
  |---|---|
  | Luna, AQUÍ ~2 s | se abre sola: «nuestro satélite · en Leo», «Luna menguante, iluminada al 12 %», «Luna nueva el sáb 10 oct a las 17:50», «A 371 600 km» |
  | CERRAR y seguir en la Luna | no se reabre |
  | Júpiter, AQUÍ | se abre la suya: «en Leo», «A 874 millones de km», «hace 49 minutos»; se cierra con Atrás |
  | «¿QUÉ ES?» apuntando a Marte | VER FICHA → «en Cáncer», «A 243 millones de km», «hace 13 minutos» |
  | NOCHE con la ficha abierta | toda en rojo sobre negro, sin blancos |

  Constelaciones y distancias coinciden con la consola y con lo validado contra Horizons (decisión 014 y paso 1).
- Revisión del texto de la Luna al verlo en la ficha: se quita «Nuestro satélite.», que repetía el subtítulo («nuestro satélite · en Leo»).
- Corregido durante la prueba: el panel dejaba un hueco bajo CERRAR (un `ScrollView` ocupa toda la altura que se le permite); ahora se mide el contenido.
- Incidencia: tras cambiar de rama, la app se cerraba al arrancar con el fallo de `Theme.MaterialComponents` del paso 12 de la Fase 3.
  Bastó con borrar `bin/` y `obj/` de la app e instalar encima, **sin desinstalar**: se conservaron los ajustes (AVISOS) y las alarmas.
- Captura: `docs/images/ficha-marte.png`.
- `dotnet test` 608 tests; build Android 0 avisos.

### Paso 4 — Lunas galileanas (2026-10-07)

- Core `Cards/`: `ISolarSystemFactsService.JupiterMoons` → `JupiterMoonsFacts` (las cuatro lunas y el radio aparente de Júpiter). Decisión 039.
  - Cada luna, **vista desde el observador** en el plano del cielo, en radios ecuatoriales de Júpiter: a la derecha (hacia acimut creciente) y arriba
    (hacia el cénit), los mismos ejes que el HUD, más la profundidad (delante o detrás de Júpiter).
  - Posiciones de `JupiterMoons` de Astronomy Engine en el instante en que salió la luz de Júpiter (la función no corrige el tiempo de luz).
  - **Estado** (`GalileanMoons`): detrás de Júpiter (ocultada), en su sombra (eclipsada, con la sombra como un cilindro de su radio en dirección contraria
    al Sol), por delante (tránsito, perdida en el brillo) o visible. Por el centro de la luna frente al radio ecuatorial.
- `FactsText.JupiterMoons`: «Con prismáticos, de izquierda a derecha: Calisto, Ío, Júpiter, Europa y Ganímedes», o «de arriba abajo» cuando la fila
  está más de pie que tumbada (con Júpiter bajo al este o al oeste, como el 8 oct de madrugada); y una línea por cada luna que no se ve:
  «Ío está detrás de Júpiter», «Europa pasa por delante de Júpiter», «Ganímedes está en la sombra de Júpiter».
  «Con prismáticos», según la NASA: «Most binoculars will show at least one or two moons orbiting the planet» («Spot the King of Planets», Night Sky Network).
- App: la ficha de Júpiter añade esas líneas a «AHORA». Consola: `--card jupiter` las muestra con cada luna en radios y en segundos de arco.
- **Validación contra JPL Horizons** (Madrid; consultas desde un script de .NET en el scratchpad):
  - Posiciones: acimut y altura sin refracción de Júpiter y de las cuatro lunas, proyectados con los mismos ejes; 12 posiciones (8 oct 6:00 y 7:30, 16 nov 7:25,
    hora local) a **≤ 0,02 radios** (≤ 0,35″, con Júpiter de 17-19″ de radio). Radio aparente de Júpiter a < 0,1 %.
  - Sucesos: los del 8 al 13 oct, minuto a minuto, aplicando las mismas reglas a los datos de Horizons (distancia de cada cuerpo para delante o detrás;
    vectores de la luna y del Sol desde Júpiter para la sombra):

    | Suceso (UTC) | Horizons | CieloHud |
    |---|---|---|
    | Ío entra en la sombra, 8 oct | 07:07 | 07:07 |
    | Ío pasa de la sombra a detrás de Júpiter | 08:09 | 08:09 |
    | Ío reaparece | 10:28 | 10:27 |
    | Europa empieza a pasar por delante / termina | 22:19 / 01:13 | 22:19 / 01:13 |
    | Ganímedes entra en la sombra / sale, 9-10 oct | 23:25 / 03:04 | 23:24 / 03:03 |
    | Calisto entra en la sombra, 13 oct | 14:50 | 14:49 |

  - Error encontrado en la propia comparación: los eclipses salían 1-2 min antes que en Horizons, al entrar y al salir. No era el cálculo: las tablas de vectores
    de Horizons leen las horas en TDB salvo que se pida `TIME_TYPE='UT'` (69 s de diferencia). Con UT, todo coincide al minuto.
  - Quedan como tests de integración (posiciones 0,05 radios, radio 0,5 %, cada suceso con su estado 2 min antes y 2 min después).
- Probado en el OPPO: la ficha de Júpiter dice «Con prismáticos, de izquierda a derecha: Ío, Europa, Júpiter, Ganímedes y Calisto», lo mismo que la consola
  en ese instante (7 oct 10:39).
- `dotnet test` 646 tests; build Android 0 avisos.

### Paso 5 — Anillos de Saturno (2026-10-07)

- Core `Cards/`: `ISolarSystemFactsService.SaturnRings` → `SaturnRingsFacts`. Decisión 040.
  - **Inclinación vista desde la Tierra**: el ángulo entre la visual y el plano de los anillos, con el polo de Saturno de la IAU (`RotationAxis`, WGCCRE 2015).
    Signo de la IAU y de Horizons (positivo si se ve la cara norte); el `ring_tilt` de `Illumination` da el mismo valor con el signo cambiado.
  - **Tendencia**: la misma inclinación vista desde el Sol, que cambia sin vaivenes, buscada día a día hasta su próximo máximo o su próximo cero
    (~3 ms en el PC). Mes a mes la Tierra puede invertirla: del 7 oct al 6 nov 2026 los anillos pasan de 7,36° a 6,45°, aunque se estén abriendo hasta 2032.
- `FactsText.SaturnRings`: «Con un telescopio pequeño, sus anillos se ven inclinados 7°» (o «casi de canto, como una raya fina» por debajo de 2°) y
  «Se irán abriendo hasta 2032, cuando llegarán a 27°» / «Se irán cerrando hasta 2039, cuando se verán de canto» (con el mes si falta menos de un año).
  «Con un telescopio pequeño», según la NASA: con prismáticos los anillos apenas se adivinan («appearing more like "ears"»), y «Even a small telescope will
  allow you to see more details of Saturn's rings» (Watch the Skies, 24 ago 2023).
- App: la ficha de Saturno añade esas dos líneas. Consola: `--card saturn` con la inclinación con signo y la fecha del extremo.
- **Validación contra JPL Horizons**, que no da la inclinación de los anillos: se obtiene de dos formas independientes que coinciden entre sí a 0,001°.
  - Desde la Tierra: la latitud del observador sobre Saturno (cantidad 14) es **planetodética**; en un planeta tan achatado son 9,0° frente a los 7,36° reales.
    Pasada a planetocéntrica con los radios de Saturno, y también calculada con la dirección a Saturno (cantidad 1) y el polo de la IAU: 11 fechas de 2025 a 2032,
    a ≤ 0,02° de CieloHud.
  - Desde el Sol (cantidad 15, mensual): máximo de 26,73° en abril de 2032 (CieloHud: 12 abr, 26,74°); de canto entre el 1 ene y el 1 feb de 2039 (CieloHud: 22 ene)
    y entre el 1 may y el 1 jun de 2025 (CieloHud: 6 may, el equinoccio de Saturno); diferencia ≤ 0,006°.
  - Quedan como tests de integración (0,05°, y el extremo dentro del intervalo de Horizons).
- **Probado en el OPPO** (8 oct, 5:45, Saturno al OSO a 25°): la ficha dice «Con un telescopio pequeño, sus anillos se ven inclinados 7°» y «Se irán abriendo
  hasta 2032, cuando llegarán a 27°», como la consola a esa hora (−7,32°, cara sur; 26,74° el 11 abr 2032). Comprobado por el usuario.
- `dotnet test` 666 tests; build Android 0 avisos.

### Paso 6 — Dibujos calculados (2026-10-07)

- Core `Cards/` (decisión 041):
  - `ISolarSystemFactsService.Disc` → `BodyDisc`: fracción iluminada desde el observador (la misma de `MoonFacts`), y hacia dónde mira el borde iluminado
    y el polo norte (IAU) en el plano del cielo, con los ejes del HUD (0° hacia el cénit, 90° a la derecha). Los ejes del plano del cielo, antes dentro de
    `JupiterMoons`, son ahora un ayudante común.
  - `PhaseShape.LitOutline`: contorno de la parte iluminada sobre un disco de radio 1 (medio limbo y media elipse de semieje |1 − 2f|).
  - `SaturnShape`: globo achatado según la inclinación (polar 54 364 km, ecuatorial 60 268 km), anillos de 1,53 a 2,27 radios (borde interior de B y exterior
    de A, hoja de datos de los anillos de la NASA) y el lado por el que pasan por delante del globo.
- App: `CardDrawing` bajo el subtítulo de la ficha.
  - Luna, Mercurio, Venus y Marte con su fase, Saturno con sus anillos y Júpiter **como con prismáticos**: campo oscuro, Júpiter a escala con sus lunas,
    escala ajustada a la luna visible más lejana y las lunas con su nombre (si dos nombres chocan, uno pasa al otro lado del punto).
  - Cada objeto con su color, volumen de esfera y halo; en modo noche, todo en rojo.
  - **Pie fijo** con CERRAR y, si el contenido no cabe, VER MÁS / VER MENOS, que agranda la ficha hasta el alto del HUD.
- Consola: `--card` añade la fracción, el borde iluminado y el polo en los ejes del HUD.
- **Validación contra JPL Horizons** (Madrid, 7 oct): cantidades 27 (PsAng, dirección contraria al Sol vista desde el objeto) y 17 (NP.ang), que se miden desde
  el norte celeste, pasadas a los ejes del HUD con el ángulo paraláctico calculado con la declinación y el ángulo horario (cantidades 2 y 42).

  | Objeto y hora (UTC) | Iluminada CieloHud / Horizons | Borde iluminado CieloHud / Horizons | Polo |
  |---|---|---|---|
  | Luna, 8 oct 5:00 | 6,415 / 6,413 % | 200,74° / 200,77° | ≤ 0,01° |
  | Luna, 14 oct 18:30 / 22 oct 21:00 / 29 oct 3:00 | 16,158 / 87,003 / 88,161 % (Horizons 16,160 / 87,000 / 88,164) | 112,0° / 106,2° / 296,8° (Horizons 111,8° / 106,1° / 297,0°) | ≤ 0,01° |
  | Venus, 7 oct 12:00 y 17:00 | 9,314 / 9,321 % | 36,63° / 36,54° | ≤ 0,01° |
  | Mercurio, Marte, Júpiter y Saturno (dos horas) | ≤ 0,01 puntos | ≤ 0,12° | ≤ 0,01° |

  Quedan como tests de integración (fracción 0,05 puntos, ángulos 0,5°).
- Tests unitarios: el área del contorno es la fracción, la parte iluminada mira al Sol, los cuernos de una creciente, el terminador de una gibosa y de media fase,
  casos extremos; los anillos (proporción, lado de delante con cada cara, achatamiento del globo, radios).
- **Probado en el OPPO** (Android 16, Debug, de día, 7 oct):

  | Prueba | Resultado |
  |---|---|
  | Luna, 11:20 | creciente del 12 % abajo a la izquierda; la consola da el borde hacia 245° en ese instante |
  | Ficha de la Luna con el dibujo | no cabía y nadie veía que se podía desplazar: pie fijo con CERRAR y VER MÁS / VER MENOS |
  | Júpiter, 11:35 | el primer dibujo (escala fija de ±27 radios e iniciales) se veía diminuto y con las iniciales montadas; pasa a la vista de prismáticos |
  | Júpiter, 11:51 | Europa e Ío a la izquierda, Ganímedes y Calisto a la derecha, como la consola (−2,8, −2,2, +9,2 y +14,8 radios) |
  | Marte, 12:06 | anaranjado, iluminado al 90 % con la parte oscura abajo a la derecha (borde hacia 304°) |
  | Venus, 12:11 | creciente del 9 % arriba y un poco a la derecha (borde hacia 18°) |
  | NOCHE con la ficha abierta | la Luna y Venus en rojo sobre negro, sin blancos ni colores |

- **Contraste con Stellarium Web** (reloj en pausa el 7 oct a las 12:30):

  | Objeto | Stellarium | CieloHud |
  |---|---|---|
  | Luna | 12 %, az 205,37°, alt 56,38°, 371 666 km; creciente a la izquierda, cuernos arriba y abajo | 11,7 %, az 205,47°, alt 56,19°, 371 691 km; borde hacia 269° |
  | Venus | creciente fina abierta hacia arriba y algo a la derecha; medido con la línea entre los cuernos, el centro del arco hacia ~20° | 9,4 %; borde hacia 21° |
  | Marte | casi lleno; la franja menos iluminada en el borde derecho, algo hacia abajo | 90 %; borde hacia 306°, la franja oscura hacia 126° |
  | Júpiter y sus lunas | Europa e Ío a la izquierda y algo arriba; Ganímedes y Calisto a la derecha y abajo. Medido en la captura desde Júpiter: pendiente de la fila −0,27 a −0,31; distancias relativas a Ganímedes, Ío 0,14, Europa 0,36, Calisto 1,59 | pendiente −0,28 a −0,30; Ío 0,15, Europa 0,35, Calisto 1,58 |

  Las diferencias de posición y distancia de la Luna vienen de la ubicación (Stellarium, la del usuario; la consola, el centro de Madrid).
- **Saturno en el OPPO y en Stellarium** (8 oct, 5:45, al OSO a 25°): la consola da el polo norte hacia 42,4° (0° = cénit, 90° = derecha), así que los anillos
  cruzan de arriba a la izquierda hacia abajo a la derecha, a unos 42° de la horizontal, casi cerrados y por la cara sur; en la captura del usuario, unos 42°.
  Stellarium Web a la misma hora, con montura acimutal: igual (comprobado por el usuario).
- Capturas: `docs/images/ficha-jupiter.png`, `ficha-venus.png` y `ficha-marte-dibujo.png` (también en el README).
- `dotnet test` 708 tests; build Android 0 avisos.

### Paso 7 — Altura y velocidad de la ISS (2026-10-07)

- Core `Cards/` (decisión 043): `ISatelliteFactsService` y `Sgp4SatelliteFactsService` → `SatelliteFacts`.
  - Altura sobre el elipsoide (conversión geodésica de SGP.NET), distancia al observador (la del HUD), velocidad inercial y periodo, todo de SGP4.
  - `SatelliteSights`: bajo el horizonte, en la sombra de la Tierra, cielo demasiado claro (Sol por encima de −6°) o visible; las reglas de un paso visible.
- `FactsText`: «A 424 km de altura y a 570 km de ti», «Va a 27 600 km/h, 7,7 km cada segundo» y una de estas cuatro:
  - «La ilumina el Sol y tu cielo está oscuro: se puede ver a simple vista»;
  - «La ilumina el Sol, pero hay demasiada luz en el cielo para verla»;
  - «Está en la sombra de la Tierra: ahora no se ve»;
  - «Ya está bajo el horizonte: ahora no se ve».
  Sin el periodo: el texto revisado ya dice «cada hora y media».
- App: la ficha de la ISS añade esas líneas en AHORA (sin dibujo). Consola: `--card iss`, con los valores en bruto y la edad del TLE.
- **Validación contra JPL Horizons** (ISS −125544, vectores geocéntricos, `TIME_TYPE='UT'`), con el TLE fijo de los tests (época 1 oct 19:41 UTC):

  | Instante (UTC) | Altura CieloHud / Horizons | Velocidad CieloHud / Horizons |
  |---|---|---|
  | 2 oct 11:26 | 428,84 / 428,83 km | 7,6574 / 7,6574 km/s |
  | 2 oct 17:56 | 422,50 / 422,55 km | 7,6629 / 7,6629 km/s |
  | 2 oct 21:00 | 424,01 / 424,07 km | 7,6622 / 7,6621 km/s |
  | 3 oct 3:00 | 429,69 / 429,68 km | 7,6581 / 7,6581 km/s |

  La altura de Horizons sale de su posición en ITRF93 (`REF_PLANE='BODY EQUATOR'`) pasada a altura sobre el elipsoide WGS-84; la velocidad, de sus vectores en ICRF.
  Periodo: 92,99 min (15,48706258 vueltas al día en el TLE). Quedan como tests de integración (altura 0,5 km, velocidad 5 m/s), con cinco instantes
  para los cuatro casos de visibilidad (el paso rasante del 2 oct a las 19:33 y la salida de la sombra del 15 oct a las 4:25).
- **Probado en el OPPO** (7 oct, 14:17, la ISS recién salida por el NO a 1° de altura, de día): «A 430 km de altura y a 2210 km de ti»,
  «Va a 27 600 km/h, 7,7 km cada segundo», «La ilumina el Sol, pero hay demasiada luz en el cielo para verla». La consola, a las 14:17:30:
  430,23 km, 2274 km, 7,6572 km/s, cielo demasiado claro (la distancia baja unos 6 km por segundo mientras se acerca). Captura: `docs/images/ficha-iss.png`.
- Pendiente en el móvil: «se puede ver a simple vista» con el paso del 13 oct (máximo a las 8:00, 36° al SE), y «en la sombra de la Tierra» del 14 oct
  en adelante, cuando la ISS sale ya alta de la sombra (por ejemplo el 16 oct, que «aparece» a las 7:12 a 19° al SO): la frase debe cambiar sola en el refresco.
- `dotnet test` 736 tests; build Android 0 avisos.

### Paso 8 — Estrellas: distancia, color y ficha en «¿qué es?» (2026-10-07)

- **Catálogo** (decisión 044): `Star` gana el número Hipparcos, la paralaje y su error (Hipparcos, van Leeuwen 2007, VizieR I/311, cruzada por posición
  a < 20″) y el tipo espectral (SIMBAD). Regenerado con un script del scratchpad que solo añade esos campos: coordenadas, magnitudes y nombres no cambian.
  - SIMBAD daba Gaia para 54 estrellas y nada para β Sco y γ¹ Leo. Comparadas con Hipparcos, 8 de esas 54 discrepan en más de 3σ:

    | Estrella | Gaia, vía SIMBAD | Hipparcos |
    |---|---|---|
    | Tarazed | 5,59 ± 0,39 mas (584 años luz) | 8,26 ± 0,17 (395) |
    | Tania Australis | 17,80 ± 0,39 | 14,16 ± 0,54 |
    | Xamidimura | 1,87 ± 0,74 | 6,51 ± 0,91 |
    | Kaus Borealis, Cor Caroli, γ Hya, ε Leo, Porrima | | 3-4,5σ |

  - En las 99 en que SIMBAD ya usaba Hipparcos, el valor es idéntico.
- Core `Cards/`:
  - `StarLight`: años luz y su margen (una desviación de la paralaje), y la certeza según el error relativo: ≤ 5 % (117 estrellas), ≤ 20 % (34),
    ≤ 50 % (Alnilam, Aludra, ο² CMa) y más (Almaaz, 84 %).
  - `StarColors`: el color por la letra del tipo espectral. Con B−V y sus límites habituales, 42 de 155 cambiaban de color (Aldebarán «rojiza»).
  - `FactsText`: «Esta luz salió de Sirio hace 8,6 años», «… hace unos 500 años», «… hace entre 1600 y 2700 años», «… hace más de 1100 años»
    (dos cifras; «más de» redondea hacia abajo); «Su luz es anaranjada»; «Brillo: magnitud 0,4 (cuanto menor, más brilla; desde ciudad se ven hasta la 3)».
- App: VER FICHA en «¿qué es?» también para las 155 estrellas. Su ficha lleva un punto de luz de su color, «DATOS» en vez de «AHORA» (no cambian
  con el momento) y esas tres líneas; sin texto todavía (paso 9). Consola: `--card <estrella>` por nombre IAU o designación («Betelgeuse», «gam Cas»).
- **Validación**:
  - Hipparcos en VizieR: los valores de los tests (Sirio, Betelgeuse, Polaris, Deneb, Alnilam y las cuatro en que SIMBAD daba otra cosa).
  - NASA: Sirio a 8,6 años luz ([Hubble](https://science.nasa.gov/asset/hubble/the-dog-star-sirius-and-its-tiny-companion/)) y Vega a 25
    ([APOD](https://science.nasa.gov/image-article/apod-1998-august-23-vega/)); CieloHud, 8,6 y 25. Betelgeuse: «unos 500» (440-570) frente a 548, 650 o 700
    según la página de la NASA; su distancia se discute de verdad, y lo dirá su texto revisado (paso 9).
  - Color: los tests fijan once estrellas conocidas (Rigel azulada, Sirio blanca, Procyon de un blanco amarillento, Capella amarillenta, Arturo anaranjada,
    Betelgeuse rojiza…) y que las 155 tienen tipo espectral con clase.
- **Probado en el OPPO** (7 oct, 16:36, de día, «¿qué es?» hacia Régulo): «Esta luz salió de Régulo hace 79 años», «Su luz es azulada» (B8),
  «Brillo: magnitud 1,4 (…)», con un punto azulado; la consola da 41,13 ± 0,35 mas, 79,3 años luz. Captura: `docs/images/ficha-estrella.png`.
- Descartado al plantearlo con el usuario: el puesto en brillo «de las que se ven desde España» (la app se puede usar en cualquier país); y queda apuntado
  que el catálogo no tiene las estrellas del sur (declinación < −50°).
- `dotnet test` 800 tests; build Android 0 avisos.

### Ajuste tras usarla: VER FICHA en la guía (2026-10-07)

- Al usar la app, la ficha que se abría sola en AQUÍ tapaba el final de la guía y, una vez cerrada, no se podía volver a abrir. Decisión 045 (corrige la 038):
  en modo guía, **VER FICHA siempre visible** para el objetivo elegido y **relleno al llegar a AQUÍ**; la ficha ya no se abre sola. En «¿qué es?», igual que antes.
  El botón se oculta con el panel de brillo del modo noche. Se quita `CardAutoOpen` de Core con sus 7 tests.
- Probado en el OPPO (16:54, guía a la Luna, al O a 14°): VER FICHA con borde bajo «derecha 2° · sube 100°» y el rótulo, sin taparlos; la ficha se abre
  antes de encontrar la Luna, se cierra y se vuelve a abrir. Al llegar a AQUÍ el botón se rellena; durante 16 s en AQUÍ (8 capturas) la ficha no se abrió sola.
  Captura: `docs/images/guia-ver-ficha.png`.
- `dotnet test` 793 tests; build Android 0 avisos.

### Paso 9 — Textos de las estrellas por lotes (desde el 2026-10-07)

Criterios en la decisión 046: lo notable de la estrella, una comparación fácil y el origen del nombre o cómo encontrarla; nada de lo que calcula la ficha
(distancia, color, magnitud); cada dato con su cita en «## Fuentes». Lotes de unos 20, cada uno en su PR con una casilla por texto: el primero, las más
brillantes o conocidas; después, por constelación o zona del cielo.

**Lote 1 (2026-10-07)**: las 15 más brillantes del catálogo (Sirio, Arturo, Vega, Capella, Rigel, Proción, Betelgeuse, Altair, Aldebarán, Antares,
Espiga, Pólux, Fomalhaut, Deneb y Régulo) y 5 conocidas por algo concreto (Cástor, Estrella Polar, Mizar, Algol y Alnilam). De 273 a 396 caracteres.
- Fuentes: las páginas *Stars* de Jim Kaler; NASA/Hubble (Betelgeuse), ESA/Hubble (Fomalhaut), NASA APOD (Polar) y la Planetary Fact Sheet (órbitas
  de Venus y Marte para comparar); RAE, «canícula», comprobada por el usuario (la RAE no deja leerse con herramientas automáticas).
- Hallazgos al contrastar: el «planeta» de Fomalhaut que da Kaler resultó en 2020 una nube de polvo (ESA/Hubble), y así lo cuenta el texto. La página del
  Hubble sobre Betelgeuse da otra distancia más, unos 725 años luz: su texto dice que no se conoce bien, sin cifra. Mizar, «la primera doble conocida (1650)»
  según Kaler, queda en «de las primeras… en el siglo XVII». La RAE no dice que «canícula» venga de Sirio, sino que en astronomía es el tiempo en que Sirio
  sale con el Sol; el texto dice eso.
- Revisión en la conversación: cinco ajustes para no decir más que la fuente (Altair gira «en 10 horas o menos», las Híades son la cabeza del Toro, Marte
  pasa «a veces» por Escorpio, Cástor está «una vez y media» más lejos que Pólux, la altura de la Polar vale «desde el hemisferio norte»).
- Test nuevo: los textos de estrellas no dicen «años luz» ni «magnitud» (lo pone el cálculo). Pendientes: 135 estrellas y 88 constelaciones.
- Consola: `--card Sirius` y `--card Polaris` muestran el texto con sus datos.
- **Probado en el OPPO** (7 oct, de día, «¿qué es?» + VER FICHA):

  | Prueba | Resultado |
  |---|---|
  | Vega, 17:29 (316 caracteres) | el texto y dos líneas de DATOS; la de la magnitud, cortada a media línea y **sin VER MÁS** |
  | Estrella Polar, 17:35, con la corrección (378 caracteres) | plegada: el texto y dos líneas de DATOS, con VER MÁS; desplegada, entera con VER MENOS |

  Corregido durante la prueba (`HudPage.FitCard`): la ficha medía su contenido con 2 dp de más de ancho, porque no descontaba el borde de 1 dp
  a cada lado. Con un texto justo por encima del límite, la medida salía una línea corta: la ficha «cabía» sobre el papel, perdía la última línea
  y no salía VER MÁS. Con el ancho bien descontado, la medida coincide con la altura real (441 dp en la Polar, comprobado con un registro temporal).
  Las fichas de la Luna y los planetas no lo notaban: pasan del límite con mucho margen.
  Captura: `docs/images/ficha-estrella-texto.png`.
- Cambio pedido por el usuario al ver las fichas: el brillo va solo con el número, «Brillo: magnitud 2,0», sin la coletilla «(cuanto menor, más brilla;
  desde ciudad se ven hasta la 3)» (decisión 046, corrige 044).
- Elegido por el usuario: en la ficha de una estrella, **DATOS va antes del texto**, para que la ficha plegada enseñe siempre sus tres datos (`CardView.FactsFirst`;
  la página mueve el texto solo cuando cambia de sitio). Probado en el OPPO (17:55): Vega plegada con el punto, DATOS con sus tres líneas y el texto entero;
  la Luna, abierta a continuación, sigue con el texto antes de AHORA. La captura `docs/images/ficha-estrella-texto.png` es la de Vega.
- `dotnet test` 794 tests; build Android 0 avisos.

Reparto de las 135 restantes en 7 lotes por zonas del cielo (acordado con el usuario el 2026-10-07): 2) circumpolares; 3) Orión, Tauro, Géminis, Auriga,
el Can Menor y la Liebre; 4) el Can Mayor, la Popa, la Vela, la Paloma y Erídano; 5) Leo, Virgo, el Boyero, el Cuervo, la Hidra, la Corona Boreal, Libra
y los Perros de Caza; 6) Escorpio y Sagitario; 7) Ofiuco, Hércules, la Serpiente, el Águila, el Cisne, el Lobo, Centauro y el Altar; 8) Andrómeda, Pegaso,
Aries, la Ballena, el Triángulo, Acuario, Capricornio, el Fénix y la Grulla.

**Lote 2 (2026-10-07)**: las 22 circumpolares. La Osa Mayor (Dubhe, Merak, Phecda, Alioth, Alkaid, Tania Australis y Psi), la Osa Menor (Kochab
y Pherkad), Casiopea (Schedar, Caph, Gamma y Ruchbah), Cefeo (Alderamin), el Dragón (Eltanin, Athebyne y Rastaban) y Perseo (Mirfak, Zeta, Épsilon, Gamma
y Delta). De 276 a 390 caracteres. Primeros textos de estrellas sin nombre IAU: el título es el que muestra el HUD («Gamma de Casiopea»).
- Fuentes: las páginas *Stars* de Jim Kaler, una por estrella.
- Revisión antes de enseñarlos: fuera «abajo» y «bajo el Carro» (Phecda, Tania Australis), que dependen de la hora (decisión 046); Psi de la Osa Mayor,
  «lo corriente a veces es una ventaja» como dice Kaler, no «sirve de referencia»; el «próximo eclipse» de Gamma de Perseo que da Kaler (2019) no se usa.
  Athebyne: Kaler la llama Al Dhibain y no explica el nombre IAU; el texto solo dice que los árabes llamaban «las dos hienas» a la pareja que forma con Zeta.
- Aprobados por el usuario sin cambios. Pendientes: 113 estrellas y 88 constelaciones.
- Probado en el OPPO (18:29, de día, «¿qué es?» hacia el norte): la ficha de Kochab con DATOS (130 años, anaranjada, magnitud 2,1) y su texto debajo.
  Para encontrarla hubo que calcular dónde estaba (Kochab, al N a 52°; Gamma de Casiopea, al NE a 24°): justo el caso de la idea de guiar a las estrellas (PLAN).
- `dotnet test` 794 tests; build Android 0 avisos.

**Lote 3 (2026-10-08)**: 18 estrellas de Orión (Bellatrix, Alnitak, Saiph, Mintaka y Hatysa), Tauro (Elnath, Alcyone y Tianguan), Géminis (Alhena,
Tejat y Mebsuta), Auriga (Menkalinan, Mahasim, Hassaleh y Almaaz), el Can Menor (Gomeisa) y la Liebre (Arneb y Nihal). De 225 a 380 caracteres.
- Fuentes: las páginas *Stars* de Jim Kaler, una por estrella.
- Kaler desfasado, y no se usa: Alhena como «la estrella más brillante ocultada por un asteroide» (1991), superada en diciembre de 2023 por Betelgeuse,
  ocultada por el asteroide Leona; el «próximo eclipse» de Almaaz (2009-2011), ya pasado: el texto dice «cada 27 años».
- Revisión antes de enseñarlos: fuera «unos segundos» en Alhena (la fuente no da la duración); en Almaaz, «su brillo baja a menos de la mitad» en vez de
  «se apaga» (Kaler: alrededor de una magnitud). Hatysa, Tianguan y Mahasim son nombres de la IAU que Kaler no explica: los textos no dicen qué significan.
- Aprobados por el usuario sin cambios. Pendientes: 95 estrellas y 88 constelaciones.
- `dotnet test` 794 tests; build Android 0 avisos. Sin prueba en el móvil: la ficha no cambia y los textos pasan las mismas reglas.

**Lote 4 (2026-10-08)**: 15 estrellas del Can Mayor (Adhara, Wezen, Mirzam, Aludra, Furud y Ómicron 2), la Popa (Naos, Pi y Tureis), la Vela (Gamma 2,
Suhail y Mu), la Paloma (Phact) y Erídano (Cursa y Zaurak). De 229 a 371 caracteres.
- Fuentes: las páginas *Stars* de Jim Kaler, una por estrella.
- Sin dar lo que las fuentes no fijan: la masa de Naos (de 22 a 60 soles según el estudio; Kaler revisó su distancia en 2008); quién apodó Regor
  a Gamma 2 de la Vela (Kaler solo cuenta el homenaje a Roger Chaffee). Mu de la Vela no se ve al norte de los 40° (Humanes está a 40,25°): el texto lo dice.
- Revisión con el usuario: en Phact, «la paloma» en vez de «la paloma de collar». Kaler da «the Ring Dove» y Wikipedia en español, «la paloma» sin fuente;
  «la paloma» es cierto con las dos y no obliga a elegir la especie. El resto, aprobados sin cambios. Pendientes: 80 estrellas y 88 constelaciones.
- `dotnet test` 794 tests; build Android 0 avisos. Sin prueba en el móvil, como el lote 3.

**Lote 5 (2026-10-08)**: 19 estrellas de Leo (Denebola, Algieba, Zosma y Épsilon), la Hidra (Alphard y Gamma), Virgo (Porrima y Vindemiatrix), el Boyero
(Izar, Muphrid y Seginus), el Cuervo (Gienah, Kraz, Algorab y Épsilon), la Corona Boreal (Alphecca), Libra (Zubeneschamali y Zubenelgenubi) y los Perros
de Caza (Cor Caroli). De 250 a 354 caracteres.
- Fuentes: las páginas *Stars* de Jim Kaler, una por estrella.
- No se usa de Kaler: el planeta de Algieba (anunciado en 2010; su masa depende de la de la estrella, incierta) ni la fecha de la última tormenta de las
  Leónidas (1998): el texto da la regla de los 33 años y la de 1833. En Izar, Kaler escribe «Pulcherima»: el texto dice «la llamó en latín "la más bella"».
- Aprobados por el usuario sin cambios. Pendientes: 61 estrellas y 88 constelaciones.
- `dotnet test` 794 tests; build Android 0 avisos. Sin prueba en el móvil, como los lotes 3 y 4.

**Lote 6 (2026-10-08)**: 19 estrellas de Escorpio (Shaula, Sargas, Kappa, Lesath, Larawag, Dschubba, Acrab, Fang, Paikauhale, Alniyat, Xamidimura e Iota 1)
y Sagitario (Kaus Australis, Nunki, Ascella, Kaus Media, Kaus Borealis, Albaldah y Alnasl). De 205 a 374 caracteres.
- Fuentes: las páginas *Stars* de Jim Kaler, una por estrella. Kaler llama Girtab a Theta de Escorpio, que en el catálogo es Sargas; su texto no menciona Girtab.
- Shaula: la ficha da «unos 570 años» (Hipparcos) y Kaler cita medidas más recientes de la mitad; el texto dice que su distancia se conoce mal, sin cifra,
  como en Betelgeuse.
- Revisión antes de enseñarlos: fuera «la de abajo» de Kaler (Paikauhale, Kaus Australis), que depende de la hora; fuera «hoy» en Acrab y Xamidimura
  (lo detectó la comprobación del scratchpad, con las mismas reglas que el test).
- Aprobados por el usuario sin cambios. Pendientes: 42 estrellas y 88 constelaciones.
- `dotnet test` 794 tests; build Android 0 avisos. Sin prueba en el móvil, como los lotes 3 a 5.

**Lote 7 (2026-10-08)**: 22 estrellas de Ofiuco (Rasalhague, Sabik, Zeta, Cebalrai y Yed Prior), la Serpiente (Unukalhai), Hércules (Kornephoros y Zeta),
el Águila (Tarazed y Okab), el Cisne (Sadr, Aljanah y Fawaris), Centauro (Menkent, Gamma, Eta, Zeta e Iota), el Lobo (Alfa, Beta y Gamma) y el Altar (Alfa).
De 250 a 344 caracteres.
- Fuentes: las páginas *Stars* de Jim Kaler, una por estrella. Okab es «Deneb al Okab Australis» en Kaler (la «Borealis» es Épsilon del Águila).
- Kaler se contradice sobre cuál de las alas del Cisne está al este; comprobado con las coordenadas (Aljanah, al este), los textos dicen «una de las alas».
- Gamma de Centauro, Gamma del Lobo y el Altar dicen desde qué latitud se ven (decisión 046).
- Aprobados por el usuario sin cambios. Pendientes: 20 estrellas y 88 constelaciones.
- `dotnet test` 794 tests; build Android 0 avisos. Sin prueba en el móvil, como los lotes 3 a 6.

**Lote 8 (2026-10-08)**: las últimas 20: Andrómeda (Alpheratz, Mirach y Almach), Pegaso (Markab, Scheat, Algenib, Enif y Matar), el Triángulo (Beta),
Aries (Hamal y Sheratan), la Ballena (Diphda y Menkar), Acuario (Sadalmelik y Sadalsuud), Capricornio (Deneb Algedi), el Fénix (Ankaa) y la Grulla
(Alnair, Tiaki y Aldhanab). De 270 a 357 caracteres.
- Fuentes: las páginas *Stars* de Jim Kaler, una por estrella. Kaler da Tiaki como estrella sin nombre propio: no se usa, la IAU se lo dio después.
- Hamal: «el punto donde el Sol cruza el ecuador del cielo en marzo» en vez de «equinoccio de primavera», que en el hemisferio sur es de otoño.
- Las esquinas del Gran Cuadrado de Pegaso, sin orientación («una de las esquinas»).
- Aprobados por el usuario sin cambios.

**Paso 9 cerrado (2026-10-08)**: las 155 estrellas tienen su texto, en 8 lotes y 8 PR (#32, #33 y #35-#40), todos revisados uno a uno
por el usuario. El test de pendientes de estrellas pasa a ser una regla: `EveryStar_HasAText`, que falla nombrando la estrella si alguna del catálogo
se queda sin texto. Quedan los 88 textos de las constelaciones (paso 10).
- `dotnet test` 795 tests; build Android 0 avisos.

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
