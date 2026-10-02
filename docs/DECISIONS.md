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
