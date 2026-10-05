# Estado

Fase actual: 3 (ver PLAN.md). Fases 1 y 2 completadas el 2026-10-02.

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

### Pendiente (Fase 3)

7. Validación en el cielo (Luna, luego planetas); resultados aquí.
