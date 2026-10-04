# Decisiones

Formato: contexto → decisión → alternativas descartadas.

## 001 — Sin backend
- Contexto: todos los cálculos dependen solo de hora, ubicación y datos orbitales públicos.
- Decisión: todo se calcula en el dispositivo; los TLE se cachean en fichero local.
- Alternativas: API propia (descartada: añade complejidad sin aportar nada).

## 002 — SDK fijado con global.json
- Contexto: la máquina tiene varios SDK instalados, incluido .NET 11 preview, que `dotnet` elegiría por defecto.
- Decisión: `global.json` con `10.0.401` y `rollForward: latestPatch`. Todo el proyecto en net10.0.
- Alternativas: no fijarlo (descartada: compilaciones distintas según máquina y previews inestables).

## 003 — Luna y planetas con CosineKitty.AstronomyEngine
- Contexto: hace falta acimut/altura de Luna y planetas con error < 0,5°, sin dependencias de UI y reutilizable en MAUI.
- Decisión: `CosineKitty.AstronomyEngine` 2.1.19 (MIT, netstandard2.0, sin dependencias, precisión declarada ±1' frente a NOVAS). Última publicación dic 2023; repo activo (ene 2025) y estable.
- Alternativas: implementar Meeus/VSOP87 a mano (descartada: más trabajo y más puntos de error); CoordinateSharp (descartada: solo Sol y Luna).

## 004 — SGP4 con SGP.NET
- Contexto: hace falta propagar el TLE de la ISS y obtener acimut/altura topocéntricos.
- Decisión: `SGP.NET` 1.6.0 (MIT, net10/netstandard2.0, mantenimiento activo, sep 2026). Da directamente acimut, elevación y distancia desde un observador. Se usa solo el propagador; el proveedor y la caché de TLE son nuestros.
- Alternativas: One_Sgp4 1.1.0 (válida, pero menos activa y exige más conversiones manuales); Zeptomoby.OrbitTools (sin licencia declarada); portar Vallado (innecesario).

## 005 — Altura aparente (con refracción)
- Contexto: la refracción atmosférica desplaza la altura cerca del horizonte hasta ~0,5°, justo la tolerancia objetivo.
- Decisión: Core devuelve altura aparente (con refracción), que es lo que ve el ojo. Validar contra Stellarium con atmósfera activada.
- Alternativas: altura geométrica (descartada: no coincide con lo que se ve ni con el valor por defecto de Stellarium).

## 006 — JPL Horizons como referencia de los tests
- Contexto: los tests de posiciones necesitan valores esperados independientes de la librería que se prueba. Stellarium no se puede consultar desde un script.
- Decisión: los valores de referencia de Luna, Júpiter y Saturno salen de la API pública de JPL Horizons (observador geodésico en Madrid, acimut/altura aparentes con refracción, `QUANTITIES='4,20'`). Tolerancia de los tests: 0,05° en ángulo y 0,1 % en distancia (diez veces más estricta que el objetivo del PLAN). Stellarium sigue siendo el juez de la validación manual del paso 7.
- Alternativas: valores de la propia librería (descartada: circular); copiar a mano desde Stellarium (válida, pero menos reproducible; se usa en la validación manual).

## 007 — Caché de TLE en fichero con marca de descarga
- Contexto: el TLE debe llegar solo (CelesTrak), funcionar sin red durante días y ser testeable sin red ni esperas.
- Decisión: `CelesTrakTleProvider` guarda un fichero por satélite (`tle-{norad}.txt`) con la hora de descarga en ISO-8601 más las tres líneas. Se reutiliza si tiene menos de 24 h; si no, se descarga de nuevo; si la descarga falla y hay copia vieja, se devuelve la vieja (el consumidor juzga la edad por `Tle.Epoch`); sin copia ni red, `TleUnavailableException`. La red se inyecta con `HttpClient` (falseable). El reloj es el del sistema: los tests de caducidad siembran el fichero de caché con la fecha de descarga que necesitan, ya que el formato es nuestro.
- Alternativas: usar la fecha de modificación del fichero (descartada: menos explícita y frágil al copiar ficheros); inyectar un `TimeProvider` (descartada: añade un parámetro al constructor solo para tests, y sembrar el fichero es igual de claro); el `CachingRemoteTleProvider` de SGP.NET (descartado: no controla la ruta ni el comportamiento sin red).

## 008 — Altura del Sol geométrica (sin refracción)
- Contexto: la Fase 2 necesita saber si el observador está a oscuras (Sol por debajo de -6°, crepúsculo civil). Los crepúsculos se definen sobre la altura geométrica del centro del Sol; la refracción desplaza ~0,6° cerca del horizonte, lo que equivale a varios minutos.
- Decisión: `ISunService` devuelve altura geométrica (`Refraction.None`). El Sol no se muestra nunca al usuario, así que la decisión 005 (altura aparente para lo que se mira) no aplica. Umbral de oscuridad: -6°, el del PLAN.
- Alternativas: reutilizar la altura aparente (descartada: desplazaría el crepúsculo ~3 min y no coincidiría con Heavens-Above ni con las tablas de efemérides); umbral -12° náutico (descartado por ahora: la ISS es muy brillante y se ve en crepúsculo civil; Heavens-Above usa -6°).

## 009 — Geometría de pasos con el barrido de SGP.NET
- Contexto: hay que encontrar los intervalos en que la ISS está sobre el horizonte, con inicio, máximo y fin al segundo.
- Decisión: `Sgp4SatellitePassPredictor` usa `GroundStation.Observe(sat, desde, hasta, paso, elevaciónMínima)` de SGP.NET con paso de 10 s y resolución de 1 s, y luego calcula la posición en los tres instantes con el mismo propagador. Un paso en curso en `desde` se recorta a `desde`; uno que empieza antes de `hasta` se devuelve completo.
- Alternativas: barrido propio con bisección (descartado: es lo mismo que ya hace la librería, validada contra Horizons); paso de 60 s (descartado: un paso rasante de 3 min podría quedar mal resuelto).

## 010 — Sombra de la Tierra: umbra cónica sobre Tierra esférica
- Contexto: un paso solo se ve si la ISS está iluminada. Hay que decidir el modelo de sombra.
- Decisión: `EarthShadow.IsInUmbra(satélite, sol)` con posiciones ECI de SGP.NET (mismo sistema para ambos). Umbra cónica: radio `R − x·tan θ`, con `sin θ = (R_sol − R_tierra) / d_sol`. Tierra esférica de 6378,137 km. Fuera de la umbra se considera iluminada (la penumbra mide unos pocos km a 400 km de altura: 1-2 s de movimiento de la ISS). Posición del Sol de `SGPdotNET.Propagation.Bodies.Sun` para no mezclar sistemas de referencia con Astronomy Engine (J2000 frente a TEME: ~0,4° de diferencia).
- Alternativas: cilindro (descartado: 30 km más ancho a 7000 km, ~4 s de error en la entrada en sombra; el cono cuesta una línea más); Tierra achatada (descartado: 21 km en los polos, < 3 s, no compensa); penumbra como estado intermedio (pospuesto: si hace falta "se atenúa" en la UI se añade entonces).

## 011 — Paso visible: muestreo cada 10 s, oscuridad exigida al aparecer
- Contexto: hay que convertir un paso geométrico en "lo que se ve", combinando Sol bajo el horizonte, ISS iluminada y altura mínima. Heavens-Above (corte documentado de -6°) lista pasos de madrugada que empiezan con el Sol a -6,3° y terminan a -4,7°.
- Decisión: `VisiblePassFinder` muestrea cada paso cada 10 s (incluyendo sus extremos exactos). Para cada tramo contiguo con la ISS iluminada, la parte visible empieza en la primera muestra con el Sol ≤ -6° geométrico y dura hasta el final del tramo iluminado, aunque el Sol suba después por encima de -6° (un paso dura minutos; el Sol no sube más de 2°). Se toma el tramo visible más largo y se acepta si su altura máxima es ≥ 10°. Si el máximo geométrico cae dentro se usa exacto. Criterios en `VisibilityCriteria` (por defecto -6° / 10° / 10 s). `EndsInShadow` / `StartsFromShadow` indican si la ISS "se apaga" o "se enciende" a media travesía.
- Alternativas: exigir Sol ≤ -6° en cada muestra (descartada: truncaba los pasos de madrugada al cruzar -6° y descartaba pasos de 37° y 71° que Heavens-Above lista y que son claramente visibles); umbral -5° (descartada: Heavens-Above documenta -6° y nuestras discrepancias con ellos son casos en el filo, no reproducibles con ningún umbral); bisección para afinar al segundo (pospuesto: 10 s basta); crepúsculo náutico -12° (descartado, ver 008).

## 012 — Orientación del móvil por cuaternión del vector de rotación
- Contexto: el HUD necesita saber hacia dónde apunta la trasera del móvil (acimut y altura) con la misma convención que las posiciones del cielo. GincanaHud usa brújula + acelerómetro con un sesgo de pitch ajustado a mano; para apuntar al cielo hace falta la orientación 3D completa y sin sesgos.
- Decisión: leer el sensor de vector de rotación de Android (`OrientationSensor` de MAUI, cuaternión dispositivo→mundo) y convertirlo en Core con `OrientationMath.ToPointing`: la dirección es el eje -Z del móvil (hacia donde mira la cámara trasera) llevado al sistema mundo (X este, Y norte, Z arriba); acimut = atan2(este, norte), altura = asin(arriba). `RollDegrees` da el giro de la pantalla alrededor del eje de puntería para mantener el HUD derecho. Suavizado exponencial circular en `PointingSmoother` (alfa 0,2 por defecto). Las convenciones de ejes están fijadas por tests con rotaciones conocidas.
- El vector de rotación está referido al **norte magnético**; la corrección de declinación (`GeomagneticField` de Android, ~0° en Madrid en 2026) se aplica en la app, no en Core. Pendiente de confirmar con el móvil en el paso 4.
- Alternativas: brújula + acelerómetro (descartada: el acimut de la brújula de MAUI es el del eje Y del móvil, no de -Z, y falla al inclinar; el pitch del acelerómetro mezcla roll); `GameRotationVector` (descartado: sin magnetómetro, su "norte" es arbitrario); matrices de rotación con `SensorManager.GetOrientation` (descartada: código Android específico; el cuaternión es portable a iOS).

## 013 — Catálogo de estrellas brillantes: SIMBAD, fijo en código, precesión con Astronomy Engine
- Contexto: el HUD necesita estrellas como referencias y para el modo "¿qué es eso?". Pocas, con nombre, visibles desde ciudad.
- Decisión: 20 estrellas con V < 1,65 y declinación > -50° (visibles desde Madrid) más Polaris, con coordenadas ICRS/J2000 y magnitud V del servicio TAP de SIMBAD (CDS), en `BrightStars` como datos en código. Posición: vector J2000 → `Rotation_EQJ_EQD` (precesión + nutación a la fecha) → `Horizon` con refracción normal, como los planetas. Se ignoran el movimiento propio (< 0,05° desde 2000 para estas estrellas) y la aberración anual (< 0,006°). Nombres IAU en Core; los nombres en español los pone la app.
- Validación: Horizons no calcula estrellas, así que los tests usan geometría independiente: altura de Polaris ≈ latitud (±0,75°) a cualquier hora, y culminación a 90° − |latitud − declinación| al sur o al norte según la declinación.
- Alternativas: `Astronomy.DefineStar` (descartada: solo 8 ranuras globales con estado compartido); catálogo Yale/Hipparcos completo (descartado: VISION pide pocos objetos); escribir las coordenadas de memoria (descartado: el cielo es el juez, los datos deben tener fuente).

## 014 — Constelaciones con los límites IAU de Astronomy Engine, sin su inversión de refracción
- Contexto: el modo "¿qué es eso?" debe responder siempre algo, aunque no haya un objeto del catálogo cerca. Las 88 constelaciones de la IAU cubren todo el cielo sin huecos.
- Decisión: `AstronomyEngineConstellationLocator` convierte la dirección (acimut, altura aparente) a J2000 con `Rotation_HOR_EQJ` y la busca con `Astronomy.Constellation` (límites oficiales, Roman 1987). Nombres latinos en Core; nombres en español con artículo ("la Ballena", "Orión") en la app.
- **Fallo de Astronomy Engine 2.1.19**: `VectorFromHorizon` con `Refraction.Normal` invierte la refracción con un bucle que nunca termina para algunas alturas (90° exactos, -71°). Apuntar el móvil al cénit habría congelado la app. Se quita la refracción con la fórmula directa `RefractionAngle` evaluada en la altura aparente (error ~0,01° cerca del horizonte) y se llama a `VectorFromHorizon` con `Refraction.None`. Cubierto por tests en 90°, 89,999°, -71°, -90° y un barrido de toda la esfera.
- Validación: estrellas en su constelación de Bayer; planetas y Luna contra JPL Horizons (`QUANTITIES='29'`): Saturno en Ballena (no en Piscis, como habría supuesto), Júpiter en Leo, Marte en Cáncer, Venus en Virgo, Luna en Géminis.
- Alternativas: tabla de límites propia del CDS (catálogo VI/42) (descartada: la librería ya la incluye); solo mostrar el objeto más cercano (descartada: con frecuencia no hay ninguno cerca).

## 015 — Estrellas hasta magnitud 3 y preferencia por las brillantes al reconocer
- Contexto: con 21 estrellas el modo "¿qué es eso?" no reconocía las del cinturón de Orión, el Carro o Casiopea, que se ven desde ciudad.
- Decisión: `BrightStars` pasa a 155 estrellas (V < 3,05, dec > -50°), **generado** desde SIMBAD y no tecleado. Un registro por posición (el sistema y sus componentes A/B o "01" se funden en el más brillante; así se cazó "bet Sco"/"bet01 Sco" duplicada). Nombres propios del Catálogo de Nombres de Estrellas de la IAU (WGSN) cruzados por posición a < 0,02° (130 de 155; todas las coincidencias a < 0,005°). Las 25 sin nombre IAU se muestran por su letra de Bayer en español ("Gamma de Casiopea"). Al reconocer, cada magnitud por encima de 0 cuenta como 1° más de distancia (`SkyIdentifier.DegreesPerMagnitude`): con 3-5° de error de brújula, lo que más probablemente estás mirando es lo que más brilla. Planetas, Luna e ISS no se penalizan. Solo se rotulan en pantalla las estrellas de magnitud < 1,7.
- Alternativas: hasta magnitud 4 (~500 estrellas; descartado: desde ciudad apenas se ven y con el error de la brújula habría varias candidatas por círculo); nombres de SIMBAD "NAME …" (descartado: mezclan alias populares sin criterio; la lista IAU es la oficial); la más cercana sin más (descartado: con 155 estrellas, una débil a 1° le quitaría el nombre a Betelgeuse a 3°).

## 016 — Proyección gnomónica en el HUD
- Contexto: el HUD proyectaba con una aproximación plana (desplazamiento de acimut × cos(altura), altura en lineal). Vale para un marcador, pero deforma las líneas y falla cerca del cénit: un objeto "al otro lado" del cénit salía muy desplazado.
- Decisión: `HudProjection` pasa a proyección gnomónica, la de una cámara: ejes de la cámara derecha (acimut creciente), arriba (hacia el cénit) y adelante (donde apunta el móvil); un punto se dibuja en (k·derecha/adelante, k·arriba/adelante) con k = (ancho/2)/tan(campo/2), campo horizontal de 60°. Los círculos máximos (horizonte, líneas de constelación) salen rectos. `ToScreen` devuelve null para lo que está detrás; `Project` lleva lo que está fuera de vista (también detrás) al borde conservando la dirección, para la flecha. Horizonte, escala de alturas, brújula, referencias, marcador y figuras usan todos la misma proyección. Sin compensación de giro de la pantalla (solo vertical).
- Alternativas: mantener la aproximación (descartada: figuras torcidas al apuntar alto); estereográfica (descartada: conserva ángulos pero curva las líneas rectas del cielo, y no se parece a lo que muestra una cámara).

## 017 — Figuras de constelaciones de d3-celestial
- Contexto: la IAU define los límites de las constelaciones, no sus figuras; hay que tomarlas de algún proyecto.
- Decisión: `data/constellations.lines.json` de d3-celestial (Olaf Frohn, BSD 3-Clause), convertido a C# (`ConstellationFigures.Data.cs`) por un script, no tecleado: 88 figuras, 150 trazos, 893 vértices en J2000. La Serpiente viene en dos trozos y se une bajo "Ser". Cada vértice pasa por el mismo camino J2000 → horizonte que las estrellas (`J2000Sky`). En el HUD solo se dibuja la figura de la constelación bajo la retícula, tenue, con su nombre.
- Validación: siendo de una fuente distinta a SIMBAD, se comprueba que las figuras de Orión, Osa Mayor, Casiopea, Cisne, Lira, Osa Menor y Escorpio pasan a < 0,02° de sus estrellas del catálogo.
- Alternativas: `constellationship.fab` de Stellarium (descartada: da las líneas como pares de números Hipparcos, que habría que cruzar con otro catálogo, y Stellarium es un proyecto GPL); dibujar todas a la vez (descartado: convierte el HUD en un planetario).
