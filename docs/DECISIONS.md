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

## 018 — Integración continua: solo Core y sus tests, en Linux
- Contexto: `main` está protegida y todo entra por PR; sin una comprobación automática, la protección solo garantiza que hay una PR, no que funcione.
- Decisión: workflow `.github/workflows/ci.yml` que en cada PR a `main` (y en cada push a `main`) restaura, compila en Release y pasa `tests/CieloHud.Core.Tests` en `ubuntu-latest`, con el SDK de `global.json` (`actions/setup-dotnet@v6`, `actions/checkout@v7`, versiones comprobadas el 2026-10-05). La comprobación "Tests de Core" es obligatoria para fusionar.
- La app MAUI **no** se compila en el CI: necesitaría el workload de Android (varios minutos y GB por ejecución) y no aportaría lo que importa, que es probarla en el móvil. Se compila y prueba a mano antes de fusionar, y la PR lo indica en su lista de comprobación.
- Alternativas: compilar también la app en `windows-latest` con el workload MAUI (descartado por ahora: lento y caro para un proyecto personal; reconsiderar si la app gana lógica no cubierta por Core); runners propios (descartado: innecesario).

## 019 — Modo nocturno rojo: paleta por funciones, barras ocultas y brillo de ventana al mínimo
- Contexto: de noche, una pantalla con blancos, verdes y cianes rompe la adaptación del ojo a la oscuridad (unos 20 minutos en conseguirse). Los astrónomos aficionados usan luz roja y tenue. Los colores estaban repartidos por `HudDrawable`, los chips y los XAML.
- Decisión:
  - `HudPalette` (record en la app) con los colores **por función** (fondo, texto en cuatro niveles, fijado, guía, marcador, alerta, estrella…) y dos instancias: `Normal` (la estética de GincanaHud, sin cambios) y `Night`. La nocturna es fondo **negro puro** (en OLED, píxel apagado) y cinco niveles de rojo con verde y azul casi a cero (`#E8301C` → `#50100A`). Los estados se distinguen por intensidad y grosor: en objetivo, rojo más vivo y trazo de 3 px con pulso; buscando, rojo medio y 1,5 px; el aviso de calibración, el rojo más vivo parpadeando.
  - `NightMode` (singleton) guarda la elección en `Preferences` y publica la paleta como recursos de la aplicación; los XAML usan `DynamicResource`, así que las páginas cambian en el acto. El lienzo y los chips leen `NightMode.Palette`. Botón NOCHE en el pie del HUD, relleno cuando está activo.
  - **Barras del sistema ocultas** en modo nocturno (modo inmersivo, reaparecen un momento deslizando desde el borde). En Android 15+ la app ocupa toda la pantalla y no se puede dar color a las barras; lo que molesta son sus iconos blancos y de colores (hora, batería verde, navegación).
  - **Brillo solo de la ventana** (`WindowManager.LayoutParams.ScreenBrightness`) a **0,01**, el fondo del rango. No toca el ajuste del sistema y Android lo deshace al salir de la app o volver al modo normal. Se re-aplica cada vez que la app vuelve al frente.
- Por qué 0,01 y no el 5 % propuesto al principio: en el OPPO, `screen_brightness` vale 1147 (escala propia del fabricante, no 0-255) y el brillo automático está activo, así que no hay forma fiable de leer "el brillo actual" para no subirlo. Además, con 0 lux el brillo automático baja a ~2 % del rango: un 5 % fijo lo **subiría** a oscuras. El mínimo del rango nunca es más brillante que lo que tenga el usuario. 0 se evita porque algunos dispositivos lo interpretan como "pantalla apagada".
- Alternativas: un filtro rojo semitransparente encima de todo (descartado: tiñe pero no apaga, los blancos quedan rosa brillante); dejar que lo haga el sistema (modo "luz nocturna" o escala de grises de Android; descartado: no es rojo, hay que ir a ajustes cada vez y afecta a todo el móvil); colorear las barras del sistema (descartado: no es posible con edge-to-edge en Android 15+); cambiar el brillo del sistema con `WRITE_SETTINGS` (descartado: permiso especial, y si la app se cierra mal el móvil se queda a oscuras).

## 020 — Fase 5: fichas escritas y datos calculados, sin IA en la app
- Contexto: la Fase 5 era "IA como guía": un modelo de lenguaje explicando el objeto encontrado. Al llegar el momento de concretarla, choca con los principios del proyecto.
- Decisión: la Fase 5 pasa a **fichas del objeto**: un texto breve por objeto, escrito y revisado, guardado en el repo como datos, más datos del momento calculados en local (fase de la Luna, lunas de Júpiter, anillos de Saturno, tiempo de luz de planetas y estrellas, altura y velocidad de la ISS). La IA puede ayudar a redactar los borradores durante el desarrollo, como un script generador más; lo que llega a la app está revisado y es fijo.
- Por qué:
  - **Sin backend** (decisión 001): un modelo en la nube necesita una clave de API, y en el APK de un repo público quedaría expuesta; esconderla exige un servidor propio. Un modelo en el móvil es pesado y, al tamaño que cabe, explica peor.
  - **El cielo es el juez**: los datos calculados se contrastan con Horizons o Stellarium; un texto generado en el momento no se puede validar y puede inventar datos.
  - **No hace falta**: el usuario no escribe nada y el universo es cerrado y pequeño (7 objetivos, 155 estrellas, 88 constelaciones). Es contenido que se escribe una vez.
  - Se usa en el campo y de noche, a menudo sin cobertura, y cada consulta a un modelo costaría dinero.
- Alternativas: LLM en la nube a través de un proxy propio (descartado: backend, coste y datos sin validar); modelo en el dispositivo (descartado: tamaño y calidad); quitar la fase (descartado: el "qué estás viendo" sigue siendo la recompensa al encontrar algo).

## 021 — Avisos de pasos: qué y cuándo en Core, sin molestar de madrugada
- Contexto: la Fase 4 avisa en el móvil antes de cada paso visible de la ISS, con la app cerrada. Qué pasos se avisan, a qué hora y con qué texto no depende de Android y debe poder probarse.
- Decisión: `Core/Alerts`. `PassAlertPlanner.Plan(pasos, época del TLE, ahora, zona horaria, ya avisados)` devuelve los avisos ordenados; Android solo los programa y los muestra. Reglas fijas, sin ajustes para el usuario (`AlertSettings`):
  - Aviso **10 min** antes del inicio visible: da tiempo a salir y calibrar la brújula.
  - **Horas de silencio 0:00-7:00** (hora local del aviso, no del paso): el aviso pasa a las **22:00 de la víspera**, «Mañana a las 6:24 pasa la ISS…». En octubre casi todos los pasos son de madrugada.
  - Si la hora del aviso ya pasó (app abierta tarde, recálculo) y el paso no ha empezado, avisa **ya**, salvo en horas de silencio, donde se omite.
  - No se avisan pasos a más de **4 días** de la época del TLE (los reimpulsos de la ISS desplazan la órbita) ni pasos ya avisados: se identifican por su inicio visible con **±2 min**, porque un TLE nuevo mueve el mismo paso unos segundos.
  - La zona horaria se pasa como parámetro (`TimeZoneInfo`); el cambio de hora del 25 oct 2026 tiene test.
- **Primer texto en español en Core** (`PassAlertText`): hasta ahora los nombres en español eran cosa de la consola y la app. El texto del aviso es lógica que debe probarse sin Android, así que va a Core. Redondeo «a la mitad hacia arriba» (2,5 min = 3 min), no al par.
  Formato: «A las 21:43 pasa la ISS · 5 min · aparece por el NO, máximo 67° al SE». Si sale de la sombra a media altura, «aparece a 19° al SO»; si se enciende ya en lo más alto, «aparece a 37° al SE y va bajando».
- Alternativas: avisar a la hora que toque aunque sea de madrugada, respetando el «No molestar» del sistema (descartada por el usuario: no molestar de madrugada); antelación configurable (descartada: principio de cero entrada de datos); agrupar en una sola notificación los pasos de una misma madrugada (pospuesto: son raros y dos notificaciones a las 22:00 se entienden); textos en la app con Core devolviendo solo datos (descartado: el texto quedaría sin tests).

## 022 — Avisos en Android: AlarmManager y notificaciones nativas, sin paquetes
- Contexto: los avisos deben llegar con la app cerrada y el móvil en reposo (Doze), en Android 16, sin backend y sin que el usuario configure nada salvo encenderlos.
- Decisión:
  - **Una sola alarma** de `AlarmManager` (`RTC_WAKEUP`), siempre la del siguiente aviso. Al saltar, `AlertReceiver` (con `goAsync`) publica los avisos que tocan, los apunta como avisados y vuelve a planificar con Core, que arma la siguiente. Sin lista de alarmas que cancelar.
  - **Exacta si el usuario lo permite**: `setExactAndAllowWhileIdle` con `SCHEDULE_EXACT_ALARM`, que desde Android 14 viene denegado y se concede en «Alarmas y recordatorios»; la app ofrece abrir esa pantalla al encender AVISOS. Sin él, `setAndAllowWhileIdle`: también salta en Doze, pero el sistema puede retrasarla unos minutos. `USE_EXACT_ALARM` se descarta: Google Play lo reserva a despertadores y calendarios.
  - **`POST_NOTIFICATIONS`** (Android 13+) se pide al pulsar AVISOS, no al arrancar. Sin él, los avisos se quedan apagados. **Desactivados por defecto**.
  - Canal «Pasos de la ISS» con importancia **alta** (aparece como banner): un aviso de 10 minutos que pasa desapercibido no sirve. El usuario puede bajarla en los ajustes del canal.
  - La notificación desaparece sola al acabar el paso (`setTimeoutAfter`). Al tocarla se abre `MainActivity` con el extra `cielohud.target=ISS` y el HUD selecciona la ISS (`LaunchRequests`), también si estaba en Diagnóstico.
  - **Ubicación guardada**: el HUD guarda en `Preferences` la última posición obtenida (`ObserverStore`), y la planificación en segundo plano usa esa. Leer la ubicación en segundo plano exigiría `ACCESS_BACKGROUND_LOCATION`, un permiso intrusivo. Contrapartida: si viajas sin abrir la app, los avisos siguen siendo los del sitio anterior.
  - Los avisos planificados se guardan con su texto ya escrito (`ScheduledAlert`, JSON con generación de código para que funcione con el recorte de Release), porque al saltar la alarma la app suele estar cerrada.
  - Se vuelve a planificar al abrir la app (activación de la ventana), al obtener ubicación en el HUD y cada vez que salta la alarma.
  - En Debug, «PROBAR AVISO (30 s)» en Diagnóstico arma una alarma real con el texto del siguiente aviso, marcada «[PRUEBA]».
- Alternativas (versiones comprobadas en NuGet el 2026-10-05): `Plugin.LocalNotification` 14.1.2 (descartado: programa notificaciones fijas, pero no recalcula en segundo plano, así que harían falta igualmente receptores propios); `Shiny.Notifications` 5.8.1 (descartado: un framework entero para tres clases); WorkManager, `Xamarin.AndroidX.Work.Runtime` 2.11.2.1 (descartado para el aviso, porque no da horas exactas; queda de reserva para el recálculo diario si la alarma resultara poco fiable). `NotificationCompat` ya viene con MAUI (`Xamarin.AndroidX.Core` 1.16.0.3). `setAlarmClock` (descartado: pone el icono de despertador en la barra de estado).

## 023 — ColorOS retrasa las alarmas exactas: la alarma se arma por pasos
- Contexto: medido en el OPPO (ColorOS, Android 16). Una alarma pedida con `setExactAndAllowWhileIdle` **y** con `SCHEDULE_EXACT_ALARM` concedido (`exactAllowReason=permission` en `dumpsys alarm`) recibe una **ventana de entrega del 75 % del retraso, con un máximo de 1 h**, y salta al final de esa ventana. Prueba a 30 s: ventana de 22,5 s, saltó a +22,5 s. Las alarmas de la app de Google con el mismo permiso tienen la misma ventana de 1 h. Solo el reloj y el calendario del sistema (`policy_permission`, es decir `USE_EXACT_ALARM` o sistema) tienen ventana 0. «Permitir actividad en segundo plano» mete la app en la lista blanca de Doze pero **no** quita la ventana. Un aviso armado con días de antelación podría llegar hasta una hora tarde, después del paso.
- Decisión: `Core/Alerts/AlarmSteps`. Se pide la alarma lo bastante pronto para que el **final** de su ventana caiga justo en la hora del aviso: con más de 140 min por delante, 1 h antes; si no, a 1/1,75 del tiempo que falta. Si salta antes de tiempo, se vuelve a armar para lo que queda, sin recalcular nada; a 30 s o menos del objetivo, se publica el aviso. En un móvil que respeta las alarmas exactas, los pasos convergen en el objetivo en unos pocos despertares (test: desde 3 días, como mucho 10, salte la alarma al principio, a la mitad o al final de la ventana). El modelo (75 %, 1 h) queda en constantes con tests; si ColorOS cambia, se ve en Diagnóstico («Última alarma: hora real · objetivo»).
- Medido en el OPPO tras el cambio: prueba a 5 min con la app cerrada desde recientes, alarma pedida a +2 min 52 s con ventana de 2 min 8 s, saltó a la hora objetivo con 0 s de diferencia; prueba a 30 s, +0 s.
- Alternativas: `setAlarmClock` (puntual también en ColorOS, pero pone el icono de despertador y aparece como «próxima alarma» en la pantalla de bloqueo, en lugar de la del usuario: invasivo); `USE_EXACT_ALARM` (probablemente exacto en ColorOS, pero Google Play lo reserva a despertadores y calendarios); pedir al usuario que cambie ajustes de batería (no quita la ventana); aceptar el retraso (descartado: el aviso de 10 min podría llegar después del paso).

## 024 — Avisos con la app cerrada: recálculo diario, eventos del sistema y lo que hace ColorOS
- Contexto: las alarmas de una app desaparecen al reiniciar el móvil y con «Forzar detención». El TLE caduca y los pasos van entrando en el horizonte de 3 días. Todo eso tiene que ocurrir sin abrir la app.
- Decisión:
  - **Recálculo diario**: una segunda alarma, **inexacta** (`setAndAllowWhileIdle`; no necesita el minuto exacto), 24 h después de cada planificación; 3 h si no se pudo calcular (sin ubicación o sin TLE). Intervalos en `AlertSettings` (`RefreshInterval`, `RetryInterval`), con la condición de que el recálculo sea más corto que el horizonte, para que un paso siempre se planifique antes de su hora de aviso.
  - **`SystemEventsReceiver`** (exportado, porque los envía el sistema) vuelve a planificar tras `BOOT_COMPLETED`, `MY_PACKAGE_REPLACED`, `SCHEDULE_EXACT_ALARM_PERMISSION_STATE_CHANGED` y `TIMEZONE_CHANGED` (al viajar cambian la hora local del texto y las horas de silencio). Permiso `RECEIVE_BOOT_COMPLETED`. Solo `BOOT_COMPLETED` y no `LOCKED_BOOT_COMPLETED`: `Preferences` vive en el almacenamiento cifrado, que no está disponible hasta el primer desbloqueo.
  - Cada planificación queda en un **historial** corto con su motivo («al abrir la app», «recálculo diario», «tras reiniciar»…), visible en Diagnóstico. Al volver a la app también se recalcula, así que sin el motivo no se distinguiría una planificación en segundo plano de una normal.
- Medido en el OPPO (ColorOS, Android 16), con CieloHud en «Permitir actividad en segundo plano»:
  - Actualizar la app: `MY_PACKAGE_REPLACED` llega con la app cerrada y planifica en unos 7 s (Debug).
  - Recálculo con la app cerrada desde recientes: la alarma arranca el proceso y planifica.
  - «Cerrar» en recientes mata el proceso, pero **no** detiene la app: las alarmas siguen.
  - **«Forzar detención» borra las alarmas** y deja la app detenida, como documenta Android; ninguna app puede evitarlo. Al abrirla de nuevo, Android 15+ le envía `BOOT_COMPLETED` y se vuelve a armar todo.
  - Reinicio: ColorOS entrega `BOOT_COMPLETED` con **retraso**, alrededor de 1 min después de desbloquear. La planificación tardó 33 s con el móvil recién arrancado (Debug); el límite de Android para un receptor en segundo plano es de 60 s.
  - El botón de la escoba de recientes no cierra apps: abre el limpiador de almacenamiento.
- Alternativas: WorkManager periódico para el recálculo (descartado: añade un paquete y, en ColorOS, sufre las mismas restricciones que una alarma; queda de reserva); recalcular solo al abrir la app (descartado: sin abrirla durante días, el TLE envejece y los pasos nuevos no se programan); `LOCKED_BOOT_COMPLETED` (descartado: las preferencias no se pueden leer antes de desbloquear).

## 025 — Conjunciones Luna–planeta: criterio y búsqueda por muestreo topocéntrico
- Contexto: la Fase 4 avisa también de «eventos destacados», empezando por la Luna junto a un planeta. Hay que decidir qué cuenta como conjunción, cómo encontrarla y qué hora recomendar, sin entrada manual.
- Decisión: `Core/Conjunctions`, `ConjunctionFinder` con criterios fijos (`ConjunctionCriteria`):
  - **Separación ≤ 5°** (unos tres dedos con el brazo estirado), **los dos a ≥ 10°** (el mismo mínimo que la ISS), **Sol ≤ -6°** (decisión 008).
  - **Venus, Marte, Júpiter y Saturno.** Mercurio fuera: en un año, con este criterio, no sale nunca; relajándolo, solo a 6-7°, a 9° de altura, de magnitud 0,4-1,6 y en pleno crepúsculo.
  - **Muestreo cada 5 min** de las horas oscuras, con la separación entre las posiciones **aparentes topocéntricas** (`ISolarSystemService`, con refracción): lo que ve el ojo. La paralaje de la Luna llega a 1°, comparable al umbral. La separación relativa cambia ~0,5° por hora: 5 min son 0,04°.
  - **Ventana** = muestras seguidas que cumplen todo. **Mejor momento** = la menor separación entre las muestras con los dos a ≥ 20° (si no las hay, entre todas), y dentro de la tarde (antes de la medianoche local) si la ventana tiene parte de tarde: una hora a la que se está despierto. El mínimo de separación cae a menudo en un borde, con la Luna a 10° o al amanecer (20 feb 2027: mínimo a las 6:30 a 12°; mejor momento a las 23:55 a 62° con 3,8°). La máxima aproximación se conserva aparte.
  - **Un acercamiento, una conjunción**: con ≤ 5° y la Luna avanzando 13° al día no puede haber dos tardes seguidas, pero sí la madrugada y la tarde del mismo día; se queda la de menor separación.
- Frecuencia (12 meses desde Humanes, oct 2026-oct 2027, Sol ≤ -6°):

  | Separación | ambos ≥ 10° | ambos ≥ 5° |
  |---|---|---|
  | ≤ 3° | 7 | 8 |
  | ≤ 4° | 10 | 11 |
  | ≤ 5° | 16 (1,3/mes) | 17 |
  | ≤ 6° | 21 | 24 |

  Con 3° se queda corto (7 al año); con 6° empieza a ser casualidad. Bajar la altura a 5° solo añade casos tras los edificios.
- Validación: JPL Horizons, 7 conjunciones de oct 2026 a ene 2027, separación ≤ 0,0044° en todas las muestras de cada ventana y ventanas idénticas (STATUS, paso 5).
- Alternativas: búsquedas de Astronomy Engine (descartadas: `SearchRelativeLongitude` es para planetas frente a la Tierra, y la conjunción en longitud geocéntrica no es la mínima separación vista desde el observador); separación geocéntrica (descartada: hasta 1° de error por la paralaje lunar); muestreo cada minuto o bisección (innecesario: la hora se redondea en el texto y 5 min son 0,04°); mejor momento = mínima separación sin más (descartada: a menudo a 10° de altura o de madrugada); mejor momento = mayor altura (descartada: elige horas de madrugada en ventanas largas y se aleja de la conjunción).

## 026 — Un planificador de horarios común; avisos de conjunción
- Contexto: los avisos de conjunción siguen las mismas reglas de horario que los de la ISS (decisión 021). Un segundo planificador las habría duplicado, y la app necesita una lista común de avisos ordenada por hora.
- Decisión:
  - `AlertPlanner` en Core: recibe candidatos (`AlertCandidate<T>`: hora deseada y hasta cuándo merece la pena) y decide cuándo suenan, con silencio de 0:00 a 7:00 → 22:00 de la víspera, aviso tardío → ya salvo de madrugada, y orden por hora. Cada tipo de aviso decide qué merece aviso y qué está ya avisado. `PassAlertPlanner` delega en él sin cambiar su comportamiento: sus tests siguen igual.
  - Conjunción (`ConjunctionAlertPlanner`): aviso **30 min antes de que se abra la ventana**. Al anochecer da tiempo a planear la noche; no hace falta la precisión de la ISS, porque dura horas. Merece la pena hasta que se cierra.
  - **Ventanas que se solapan, un solo aviso** («la Luna junto a Júpiter (4°) y Marte (4°)»). La misma noche, pero en momentos distintos, son dos avisos, porque son dos horas distintas.
  - **No repetir**: misma conjunción = mismo planeta con mejor momento a menos de 1 día. Las efemérides son deterministas, así que basta con el planeta y el mejor momento; el día de margen cubre la madrugada y el anochecer del mismo acercamiento cuando se planifica entre las dos.
  - Texto (`ConjunctionAlertText`): «Esta noche / Esta madrugada / Mañana temprano / Ahora, la Luna junto a Júpiter (3°) · mejor hacia las 22:00 al SE». Hora al cuarto de hora («hacia»): en 15 min la separación cambia ~0,1°. Dirección de la Luna, porque al tocar el aviso el HUD guía a la Luna.
- Alternativas: dos planificadores con una clase común para las horas de silencio (descartada: la regla del aviso tardío y el orden también se repetirían, y la lista común quedaría en la app sin tests); aviso a hora fija, por ejemplo a las 20:00 (descartada: el anochecer va de las 18:00 a las 22:00 según la época, y las ventanas que empiezan tarde quedarían lejos); avisar al mejor momento (descartada: para una ventana de horas, mejor avisar cuando empieza); un aviso por planeta aunque coincidan (descartada: dos notificaciones para mirar al mismo sitio).

## 027 — Avisos de conjunción en Android: lista común, canal propio, toque a la Luna
- Contexto: la app solo sabía avisar de pasos de la ISS (decisiones 022-024). Las conjunciones tienen que entrar en la misma cadena de una sola alarma sin romperla, y no dependen de la red.
- Decisión:
  - **Un servicio, una lista**: `AlertService` (antes `PassAlertService`) planifica los dos tipos con Core (decisión 026), los guarda juntos ordenados por hora y arma la alarma al primero. Al saltar publica todo lo que toca, sea del tipo que sea. AlarmSteps y los receptores no cambian.
  - **Tipo en el aviso guardado**: `ScheduledAlert.Kind`, con `Pass` = 0 para que el JSON guardado por la versión anterior se lea como paso. «Merece la pena hasta» es el inicio del paso o el final de la ventana; el objetivo al tocar, «ISS» o «Luna».
  - **Avisados**: lista aparte de `NotifiedConjunction` (planeta y mejor momento), que se poda a los 2 días.
  - **Sin TLE no se pierde todo**: las conjunciones solo necesitan las efemérides y la última ubicación. La falta de órbita de la ISS queda como problema visible y adelanta el recálculo a 3 h.
  - **Canal propio, «Luna y planetas», con importancia normal** (suena, sin banner). Se avisa con antelación de algo que dura horas; el de la ISS sigue en alta, porque son 10 minutos. Cada canal se ajusta por separado en el sistema.
  - **Al tocar, la Luna**: el planeta está a pocos grados, y la Luna es lo más fácil de encontrar. El HUD ya resolvía el objetivo por nombre (`LaunchRequests`).
  - Icono monocromo propio (luna creciente). ColorOS lo sustituye por el de la app, como con la ISS.
- Alternativas: un servicio por tipo de aviso con dos alarmas (descartado: rompe la cadena de una sola alarma y duplica los pasos de AlarmSteps); el mismo canal para todo (descartado: un aviso de horas con banner molesta, y el usuario no podría silenciar uno sin el otro); guiar al planeta al tocar (descartado: si el planeta es débil, como Saturno o Marte lejano, cuesta más que la Luna, y desde ella se ve al lado); no planificar nada sin red, como antes (descartado: las conjunciones no la necesitan).

## 028 — Planetas juntos: ≤ 3°, una conjunción por acercamiento, la noche más cercana
- Contexto: además de la Luna junto a un planeta, dos planetas juntos son un evento a simple vista, raro y llamativo (Marte y Júpiter a 1,2° el 16 nov 2026). A diferencia de la Luna, se acercan despacio: la pareja está cerca muchas noches seguidas.
- Decisión:
  - **Criterio**: separación ≤ 3°, los dos a ≥ 10°, Sol ≤ -6°; Venus, Marte, Júpiter y Saturno, sin Mercurio. Es más estricto que con la Luna (5°), porque dos puntos de luz a 5° no se ven «juntos». Sondeo de dos años desde Madrid: solo dos acercamientos, Marte–Júpiter (nov 2026) y Venus–Marte (sep 2028). Bajando la altura a 5° aparecen 3-4 más, todos a 5-8° en pleno crepúsculo.
  - **Una conjunción por acercamiento**: las noches seguidas a ≤ 3° forman uno, y la conjunción es su noche más cercana, con todas las noches juntos (`Nights`). Para saber cuál es la más cercana se mira el acercamiento entero, buscando 60 días antes y después. Un filtro diario (separación a mediodía ≤ 3° + 2°) limita el muestreo fino a las noches que pueden cumplir.
  - **Mejor momento: la mayor altura** del más bajo de los dos, con la misma preferencia por la tarde que la Luna. En una noche la separación apenas cambia (~0,02° por hora), así que la mínima separación no sirve para elegir.
  - **Guía: el más brillante**, por un orden fijo (Venus, Júpiter, Marte, Saturno). Marte supera a Júpiter unas semanas cada dos años, pero el otro sigue estando al lado.
  - Modelo común: `Conjunction` con guía y compañero; la Luna es un guía más. Sirven el mismo planificador, el mismo texto y la misma app.
- Validación: JPL Horizons, Marte–Júpiter: la misma noche más cercana, la misma ventana y separación ≤ 0,0006° noche a noche (STATUS, paso 8).
- Alternativas: un aviso cada noche que estén cerca (descartada: 15 avisos seguidos); avisar la primera noche (descartada: la más cercana es la que merece la pena, y el texto ya dice qué noches se ven juntos); 5° como con la Luna (descartada: no se ven «juntos»); conjunción geocéntrica en ascensión recta o longitud con las búsquedas de Astronomy Engine (descartada: puede caer de día; lo que importa es la noche visible); acercamientos de meses, como Júpiter–Saturno cada 20 años (fuera: harían falta más de 60 días de búsqueda; el próximo es en 2040).

## 029 — Avisos de planetas juntos: solos, una vez por acercamiento, el toque al más brillante
- Contexto: el buscador ya da la noche más cercana de cada acercamiento de dos planetas (decisión 028). Falta decidir cómo entra en los avisos de conjunción (decisión 026) y en la app (decisión 027).
- Decisión:
  - **Mismo horario que la Luna**: 30 min antes de que se abra la ventana de la noche más cercana; de madrugada, a las 22:00 de la víspera.
  - **Siempre solos**: no se agrupan con una conjunción de la Luna que coincida en hora. Son cosas distintas que mirar, y el texto de cada una ya es largo.
  - **Una vez por acercamiento**: no se repite la misma pareja a menos de 30 días del mejor momento ya avisado. Planificando desde otro sitio la noche más cercana podría cambiar, pero no deja de ser el mismo acercamiento.
  - **Texto**: «Mañana temprano, Marte junto a Júpiter (1°), lo más cerca en estas semanas · mejor hacia las 7:30 al S · juntos del 9 al 23 nov». El intervalo es para quien no pueda esa noche (nubes): se ven juntos muchas más. El más débil «junto al» más brillante, que es el que se busca.
  - **Al tocar, el planeta más brillante** (`ScheduledAlert.Guide`). Mismo canal, «Luna y planetas».
- Alternativas: agrupar con la Luna como dos planetas a la vez (descartada: mezclaría «la Luna junto a…» con una pareja de planetas en un texto ilegible); repetir el aviso la primera y la última noche (descartada: el intervalo en el texto basta); canal propio para los planetas (descartado: es la misma clase de aviso, y son uno o dos al año).

## 030 — Pantalla de próximos eventos: calculada al abrirla, con las reglas de los avisos
- Contexto: los avisos dicen lo que hay cuando toca; el usuario quiere poder ver lo que viene («¿qué hay esta semana?»). Lo planificado para los avisos solo existe con AVISOS activado, cubre 3 días y quita lo ya avisado.
- Decisión:
  - **Se calcula al abrir la pantalla**, con los mismos buscadores, y funciona con los avisos apagados: conjunciones a 30 días (1-3 al mes, la lista no se llena) y pasos de la ISS a 3 días (más allá el TLE no es fiable; decisión 021).
  - **La hora del aviso de cada evento sale de los mismos planificadores** (`UpcomingEvents.Build` en Core), con lo ya avisado. Lo que dice la lista y lo que hace el móvil no pueden divergir.
  - **Textos absolutos** («≈7:45», «lun 2 nov»), no relativos al aviso («mañana temprano»), porque la lista se lee en cualquier momento. Días y meses con nombres propios, sin depender de la cultura del sistema.
  - **Botón EVENTOS en el sitio de las coordenadas** del pie del HUD (opción elegida por el usuario). Las coordenadas ya estaban en Diagnóstico y así no salen en las capturas. El aviso de «sin ubicación», que iba en ese pie, pasa al HUD.
  - **Al tocar un evento, el HUD guía a su objetivo**, por el mismo camino que al tocar un aviso (`LaunchRequests`).
  - El muestreo de conjunciones pasa a **pasos fijos** (:00, :05… UTC): el resultado ya no depende del instante del cálculo.
- Alternativas: mostrar los avisos pendientes (descartada: vacía con los avisos apagados y solo 3 días); cuarto botón en el pie (descartada: no cabe bien); entrar desde el diálogo de AVISOS (descartada: escondida); una lista más larga, de 60-90 días (pospuesto: 30 días bastan para planear y la búsqueda sigue siendo rápida).

## 031 — Modo nocturno: rojos casi plenos y brillo del 10 %, ajustable (corrige 019)
- Contexto: con la paleta y el brillo de la decisión 019 el HUD no se leía en una habitación a oscuras. Medido en el OPPO: el brillo automático a oscuras es 12 de 2047 y el 1 % forzado ~20, casi igual; el problema era el color: el rojo da ~1/5 de la luz del blanco, y los botones usaban rojos del 31-63 %. Además, sin `KeepScreenOn` la pantalla se apaga a los 30 s mientras se apunta al cielo sin tocar el móvil.
- Decisión:
  - Textos y botones en rojo casi pleno (`#FF3020`, `#E62A1A`), lo decorativo en rojos medios. La oscuridad la ponen el fondo negro y el brillo bajo, no unos rojos casi negros.
  - Brillo de ventana por defecto **10 %** (el que el usuario lee bien) y **ajustable** a 3, 6, 10 o 20 %: ojos, presbicia y pantallas son distintos. Con el modo noche activado, NOCHE abre un panel propio en la paleta (un diálogo del sistema sería blanco); otro toque o «Salir del modo noche» lo cierran.
  - `KeepScreenOn` mientras el HUD está delante.
- Alternativas: no forzar el brillo (descartada: a oscuras el automático da 12, menos que el 1 % que ya no se leía); un deslizador (descartada: impreciso a oscuras y con guantes); mantener pulsado NOCHE (descartada: escondido); botones +/− en el pie (no caben); teclas de volumen (inesperado).

## 032 — APK descargable: firma propia fuera del repo, publicación a mano, Acerca de con las licencias
- Contexto: probar la app exigía el SDK de .NET y el workload de MAUI. Con un APK descargable, cualquiera puede instalarla y salir a mirar el cielo con otros.
- Decisión:
  - **Publicación a mano** en GitHub Releases con `gh release create`, marcada como *pre-release* mientras la fase esté abierta. `scripts/publish-apk.ps1` compila en Release desde limpio, firma y verifica la firma con `apksigner` (falla si no verifica o no la firma `CN=CieloHud`).
  - **Clave propia fuera del repositorio** (público): `%USERPROFILE%\.cielohud\cielohud-release.keystore` y su contraseña en un fichero al lado, con copia de seguridad del usuario. Sin ella, una versión nueva no puede actualizar la instalada (habría que desinstalar y se perderían los ajustes). En el certificado, `CN=CieloHud`, no un nombre personal: cualquiera puede leerlo en el APK.
  - **Versiones**: `ApplicationDisplayVersion` = la del README/fase (0.4.0); `ApplicationVersion` = mayor×10000 + menor×100 + parche, siempre creciente.
  - **Acerca de** en el pie en lugar de DIAGNÓSTICO: qué es, privacidad (todo en el móvil; solo se descarga la órbita de la ISS), enlace al código, datos y licencias, y desde ahí Diagnóstico. Los textos de licencia van como recursos de la app (la BSD de d3-celestial y la OFL de las fuentes piden acompañar al binario) y se muestran bajo demanda.
- Alternativas: GitHub Actions al crear una etiqueta (pospuesto: necesita el workload de MAUI en el runner, ~10 min, y la clave en los secretos; merece la pena si se publica a menudo); Google Play (descartado por ahora: cuenta de pago, revisión y política de permisos de alarmas); firmar con la clave de depuración (descartado: es pública y cambia entre máquinas); ocultar Diagnóstico solo en Release (descartado: dos pies distintos que mantener).

## 033 — Mercurio: temporadas con el criterio de siempre y magnitud ≤ 0,5
- Contexto: Mercurio estaba fuera de las conjunciones (decisión 025) y VISION lo cita «como extra». Hay que decidir con datos si merece un aviso propio. La elongación no basta: lo que cuenta es su altura con el cielo ya oscuro, y eso depende de lo inclinada que esté la eclíptica sobre el horizonte.
- Sondeo de dos años (oct 2026-sep 2028, Humanes de Madrid, cada minuto del crepúsculo):

  | Criterio | Temporadas | Días |
  |---|---|---|
  | ≥ 10° con el Sol ≤ -6° | 6 | 82 |
  | ídem y magnitud ≤ 1 | 6 | 75 |
  | **ídem y magnitud ≤ 0,5** | **6** | **68** |
  | ≥ 10° con el Sol ≤ -4° | 7 | 132 |
  | ≥ 8° con el Sol ≤ -4° | 10 | 211 |
  | ≥ 5° con el Sol ≤ -6° | 10 | 251 |

  Las elongaciones mayores no son las mejores: 12 oct 2026 al anochecer, 25°, Mercurio a 3,4° con el Sol a -6°; 17 mar 2027 al amanecer, 28°, a 5,7°; 25 sep 2027 al anochecer, 26°, a 3,2°.
  En cambio, 21 nov 2026 al amanecer, 20°, a 12,3°, y 24 may 2027 al anochecer, 23°, a 13,4°. Buenas: las tardes de primavera y de enero-febrero y las madrugadas de otoño.
- Decisión:
  - **Merece la pena**: unas 3 temporadas al año de 5 a 15 días, con Mercurio a 10-14° y de magnitud -0,6 a 0,1 en su mejor día. Mucha gente no lo ha visto nunca.
  - **Criterio**: Mercurio a **≥ 10°** con el **Sol ≤ -6°**, el mismo que la ISS y las conjunciones (decisión 008), y **magnitud ≤ 0,5**. A 10° la luz atraviesa unas cinco veces más aire que en el cénit, y desde ciudad, en el crepúsculo, un Mercurio más débil cuesta. El tope recorta la cola de las temporadas de primavera, cuando se apaga deprisa (mayo de 2027: 14 días en vez de 18); el mejor día no cambia.
  - **Ventana** de cada día: los minutos que cumplen (de 1 a 21). **Mejor momento**: Mercurio más alto, que resulta ser cuando el Sol llega a -6°: en el crepúsculo, el cielo se oscurece más deprisa de lo que Mercurio pierde por bajar.
  - **Temporada**: días seguidos con ventana, al anochecer o al amanecer; **mejor día**, el de Mercurio más alto.
  - La magnitud es la de Astronomy Engine. Frente a JPL Horizons, que usa un modelo más reciente, sale hasta 0,13 más brillante, y en mayo de 2027 eso añade un día al final (0,44 frente a 0,51). Se acepta: el tope es una regla práctica; la posición coincide en milésimas de grado (STATUS, paso 11).
- Alternativas: Sol a -4° (descartada: cielo mucho más claro; solo añade julio de 2027 al amanecer, a 9° con el Sol a -6°); 8° o 5° de altura (descartadas: Mercurio tras los edificios y la bruma, y el doble de días, casi todos malos); sin tope de magnitud o con 1 (descartadas: días al final de la temporada con Mercurio de magnitud 1-1,8, difícil a esa altura); avisar por la máxima elongación (descartada: la de octubre de 2026 no se ve); la magnitud de Mallama y Hilton (2018), la de Horizons, programada aparte (pospuesto: una fórmula más que mantener para mover un día en el borde de una temporada).

## 034 — Aviso de Mercurio: uno por temporada, en su mejor día
- Contexto: el buscador da las temporadas de Mercurio con su mejor día (decisión 033). Falta decidir cuándo avisar, con qué texto y cómo entra en EVENTOS. Una temporada dura de 5 a 15 días y su altura cambia poco de un día a otro (noviembre de 2026: de 10,1° a 12,4°), pero cada ventana dura minutos.
- Decisión (el usuario eligió el mejor día):
  - **Un aviso por temporada, en su mejor día**, como las parejas de planetas (decisión 029): 30 min antes de que se abra su ventana; al amanecer, a las 22:00 de la víspera. Mismo `AlertPlanner`. No se repite la misma franja a menos de 30 días.
  - **Texto**: «Mañana al amanecer, Mercurio a 12°, lo más alto en estas semanas · mejor hacia las 7:35 al SE · se ve del 14 al 28 nov». La altura, porque hace falta un horizonte despejado; el intervalo, por si ese día está nublado.
  - **Hora a 5 minutos**, la marca más cercana dentro de la ventana: las ventanas duran minutos, no horas, y al anochecer el mejor momento es su inicio, así que redondear hacia abajo diría una hora con el cielo aún claro.
  - **EVENTOS**: una entrada en el mejor día, «≈7:35 · Mercurio al amanecer (12°) · al SE · se ve del 14 al 28 nov», con la hora de su aviso calculada por el mismo planificador.
  - **Al tocar**, el HUD guía a Mercurio (paso 13).
- Alternativas: avisar al empezar la temporada (descartada: el primer día Mercurio apenas pasa de 10° durante 1-5 minutos, y el mejor día se quedaría sin recordatorio); los dos avisos (descartada: el doble de avisos para lo mismo); hora al cuarto de hora, como las conjunciones (descartada: cae fuera de ventanas de 6-14 minutos); minuto exacto (descartada: aparenta una precisión que el cielo no pide; «hacia» ya dice que es aproximado).

## 035 — Objetivo al tocar un aviso: un solo oyente, y la actividad nueva solo guarda la petición
- Contexto: al tocar un aviso, `MainActivity` pasa el objetivo («Mercurio», «Luna»…) al HUD por `LaunchRequests`. Cada HUD se suscribía a un evento estático y no se desuscribía. Al salir con «Atrás» la actividad se cierra pero el proceso sigue; al volver, o al tocar un aviso, se crea otra actividad con un HUD nuevo, y el viejo, invisible, se quedaba la petición: el HUD visible seguía en la Luna. Encontrado probando Mercurio en el OPPO; pasaba con cualquier aviso.
- Decisión:
  - **Un solo oyente**: el último HUD creado (`LaunchRequests.Listen`), que es el de la actividad viva.
  - **La actividad nueva solo guarda la petición** (`Keep`, desde `OnCreate`): su HUD aún no existe y la recoge al aparecer. Solo `OnNewIntent`, con la actividad viva (quizá con Diagnóstico encima), avisa al HUD abierto (`Request`).
  - El chip del objetivo se muestra cuando la barra ya tiene tamaño: en una actividad nueva aún no está maquetada al aparecer el HUD.
- Alternativas: desuscribirse al desaparecer (descartada: con Diagnóstico encima el HUD desaparece y el toque no volvería a él); desuscribirse cuando se destruye la ventana (descartada: depende del orden en que MAUI destruye y crea, que no controlamos); que el HUD sea único (descartada: MAUI crea una ventana y su página por actividad).

## 036 — Datos de la Luna y los planetas: iluminación topocéntrica, fase principal ±1 día
- Contexto: las fichas muestran la fase de la Luna y la distancia y el tiempo de luz de los planetas (Fase 5). Hay que decidir desde dónde se calculan, cuándo se llama «Luna llena» a la Luna y cómo se dicen las cifras.
- Decisión:
  - **Iluminación de la Luna desde el observador**, con los vectores Sol, Luna y observador de Astronomy Engine (`GeoVector`, `ObserverVector`): `Illumination` es geocéntrica, y la paralaje lunar mueve la fracción hasta medio punto, lo que basta para cambiar el porcentaje redondeado. Coincide con Horizons topocéntrico a ≤ 0,003 puntos; es lo que muestra Stellarium.
  - **Fase y fases principales geocéntricas** (`MoonPhase`, `SearchMoonQuarter`), como las definen los calendarios. Coinciden con Horizons a menos de 40 s.
  - **Nombre**: la fase principal («Luna llena», «Cuarto creciente»…) hasta **1 día** antes o después de su instante; el ojo no distingue un 98 % de un 100 %, ni media Luna de un poco más. Entre ellas, «Luna creciente», «Gibosa creciente», «Gibosa menguante» o «Luna menguante» según la fase. Con las fases a 6,6-8,2 días, los tramos intermedios duran al menos 4,6 días.
  - **Distancias y tiempo de luz desde el observador** (las mismas posiciones topocéntricas del HUD), velocidad de la luz exacta. Cifras para usar, no para medir: la Luna a la centena de km, los planetas al millón de km; el tiempo de luz en segundos (Luna), en minutos y segundos por debajo de 10 min, en minutos hasta la hora y en horas y minutos por encima.
  - Las frases van en Core (`FactsText`) con tests, como los textos de los avisos (decisión 021). Espacio de no separación entre miles («369 600 km») y antes de «%», como recomienda la RAE; los números de cuatro cifras van juntos («1262»).
- Alternativas: fracción geocéntrica de `Illumination` (descartada: hasta medio punto distinta de lo que se ve); nombrar la fase solo por el ángulo, con tramos de 45° centrados en cada fase principal (descartada: «Cuarto creciente» abarcaría ±1,8 días, de una Luna iluminada al 31 % a una al 69 %); la fase principal solo el día de calendario (descartada: depende de la zona horaria y la hora del instante; ±1 día es simétrico); edad de la Luna en días (descartada por ahora: la fase y la próxima fase principal ya lo dicen).

## 037 — Textos de las fichas: un Markdown por objeto, con fuentes, incrustado en Core
- Contexto: cada objeto (7 objetivos, 155 estrellas, 88 constelaciones) tiene un texto breve escrito de antemano (decisión 020). Hay que decidir dónde y cómo se guardan para revisarlos uno a uno, comprobarlos con tests y que la app los lea sin red.
- Decisión:
  - **Un fichero Markdown por objeto** en `src/CieloHud.Core/Cards/Texts/es/{targets,stars,constellations}/<id>.md`: `# Título`, el texto y `## Fuentes`. Un fichero por objeto hace que la PR se revise texto a texto (la casilla «Viewed» de GitHub) y que el diff de una corrección sea una línea legible.
  - **Identificadores estables**: el nombre del cuerpo en inglés (`Jupiter.md`), `ISS.md`, la designación de Bayer de las estrellas (`alf-CMa.md`, la misma clave que el catálogo) y el símbolo IAU de las constelaciones (`Ori.md`). El título es para quien revisa; la app muestra el nombre que ya usa.
  - **Las fuentes viven en el fichero**, no en la app: cada cifra o afirmación fija tiene que poder contrastarse. Lo que cambia con la fecha (distancias, fase, altura) no se escribe: lo pone el cálculo.
  - **Incrustados en Core** (`EmbeddedResource`) y leídos por `CardTexts`: los tests de Core, que corren en el CI, comprueban formato, longitud (≤ 400 caracteres), fuentes, texto plano, palabras que dependen del momento, que cada fichero corresponde a un objeto existente y cuántos faltan.
  - **Sin texto no se bloquea nada**: la ficha sale solo con datos. El test de pendientes se actualiza con cada lote y debe llegar a cero al cerrar la fase.
  - Los espacios dentro de números y entre número y unidad se vuelven de no separación al leer: quien escribe usa espacios normales.
- Alternativas: un JSON por categoría (descartada: comillas escapadas, líneas larguísimas y diffs ilegibles para revisar prosa); YAML con un paquete NuGet (descartada: dependencia para un formato que se lee en 60 líneas); C# generado como el catálogo de estrellas (descartada: los textos se escriben y corrigen a mano, no se generan); ficheros en la app MAUI (descartada: sin tests en el CI); recursos `.resx` (descartada: pensados para cadenas cortas de interfaz, incómodos de revisar en un diff).

## 038 — La ficha en la app: sola en AQUÍ, con un botón en «¿qué es?», panel propio
- Contexto: al encontrar algo, la ficha cuenta qué es (Fase 5). Hay que decidir cuándo aparece sin estorbar a la guía, cómo se cierra y cómo se ve de noche.
- Decisión (elegida por el usuario en el plan de la fase):
  - **Modo guía: se abre sola tras 1,5 s seguidos en AQUÍ, una vez por objetivo.** Es lo que se tarda en bajar el móvil y mirar; al volver la vista a la pantalla, la ficha está ahí. Salir de AQUÍ antes reinicia la cuenta; cerrarla no la vuelve a abrir hasta elegir otro objetivo. Al bajar el móvil se sale de AQUÍ, pero la ficha no se cierra.
  - **«¿Qué es?»: botón VER FICHA**, no apertura automática: al barrer el cielo la identificación cambia a cada momento. El botón sigue 2 s después de perder lo reconocido, porque con 3-5° de error de brújula la retícula entra y sale del objeto y el botón parpadearía bajo el dedo; otro objeto lo sustituye en el acto.
  - Las dos reglas, en Core (`CardAutoOpen`, `RecentMatch`), con tests: son tiempos y estados difíciles de probar en el móvil.
  - **Se cierra** con CERRAR, con Atrás o al elegir otro objetivo (también al tocar un aviso).
  - **Panel propio** con la paleta (`DynamicResource`), como el del brillo nocturno: un diálogo del sistema sería blanco de noche. Abajo, sin tapar la retícula; alto según el contenido, hasta 440 dp, y desplazable si no cabe (fuentes del sistema grandes). Los datos se recalculan cada 10 s mientras está abierta.
- Alternativas: abrirla al instante en AQUÍ (descartada: taparía el final de la guía y aparecería al rozar el objetivo); un botón también en AQUÍ (descartada: obliga a tocar el móvil con el brazo levantado); abrirla sola en «¿qué es?» al reconocer algo (descartada: se abriría y cerraría al barrer); otra página (descartada: se perdería el HUD de fondo y la vuelta sería más lenta).

## 039 — Lunas galileanas: vistas desde el observador, estado por su centro, sombra cilíndrica
- Contexto: la ficha de Júpiter dice dónde están sus cuatro lunas grandes («a su izquierda, tres de sus lunas», VISION). Hay que decidir en qué ejes se dan, cuándo una luna «no se ve» y cómo se dice.
- Decisión:
  - **Ejes del observador**: derecha (hacia acimut creciente) y arriba (hacia el cénit) sobre el plano del cielo, los del HUD y los de unos prismáticos sujetos derechos. Unidad: el radio ecuatorial de Júpiter (71 492 km), que no depende de la distancia; el radio aparente va aparte para el dibujo.
  - **Tiempo de luz**: las lunas se calculan en el instante en que salió la luz de Júpiter (`JupiterMoons` es jovicéntrica y no lo corrige; son de 35 a 50 min según la época, en los que Ío avanza hasta 7°).
  - **Estado por el centro de la luna** frente al radio ecuatorial: detrás (ocultada) gana a la sombra, y la sombra al tránsito. El achatamiento de Júpiter (6,5 %) y el tamaño de las lunas (< 0,04 radios) mueven un suceso uno o dos minutos; la frase no da horas.
  - **Sombra: cilindro** del radio de Júpiter en dirección contraria al Sol. El cono real se estrecha menos de un 3 % a la distancia de Calisto, y la penumbra son segundos.
  - **Texto**: la fila de lunas visibles con Júpiter en su sitio, «de izquierda a derecha», o «de arriba abajo» cuando la fila está más de pie que tumbada (Júpiter bajo al este o al oeste); y una línea por cada luna que no se ve. «Con prismáticos», porque a simple vista no se ven y la NASA dice que casi todos los prismáticos muestran una o dos.
- Validación: JPL Horizons, posiciones a ≤ 0,02 radios y ocultaciones, tránsitos y eclipses del 8 al 13 oct 2026 al minuto (STATUS, paso 4).
- Alternativas: ascensión recta y declinación (descartada: no es lo que se ve; la fila gira con la altura de Júpiter, y al salir puede estar casi vertical); segundos de arco (descartada como unidad: cambian con la distancia; se dan aparte); cono de sombra y penumbra (descartada: segundos de diferencia); horas de los sucesos en el texto (pospuesto: «Ío está detrás de Júpiter» basta para entender por qué falta una; un aviso de sucesos sería otra cosa).

## 040 — Anillos de Saturno: inclinación con el polo de la IAU y tendencia vista desde el Sol
- Contexto: la ficha de Saturno dice cómo se ven sus anillos. Su inclinación vista desde la Tierra va de 0° (de canto, en 2025 y 2039) a unos 27° (en 2032) con un ciclo de 29,5 años, más un vaivén anual por la órbita de la Tierra.
- Decisión:
  - **Inclinación desde la Tierra** con el polo de Saturno de la IAU (`RotationAxis` de Astronomy Engine) y la dirección geocéntrica aparente: la diferencia entre el horizonte de Madrid y el centro de la Tierra no se nota (la paralaje de Saturno es de 1″). Signo de la IAU (positivo, cara norte). Se descarta `Illumination.ring_tilt`, que da el mismo valor con el signo contrario y sin documentarlo.
  - **Tendencia vista desde el Sol**, no mes a mes: la de la Tierra se invierte durante semanas cada año (octubre-noviembre de 2026 se cierran, aunque vayan hacia su máximo de 2032). «Se irán abriendo hasta 2032» es lo que cualquier aficionado contará, y no cambia de un mes a otro.
  - El extremo, buscado **día a día** hasta 12 años (el ciclo es de unos 7 años en cada sentido): como mucho unos 2600 cálculos (de un cruce a su máximo), ~3 ms en el PC.
  - Texto: grados enteros sin signo (la cara norte o sur no se distingue sin experiencia), «casi de canto» por debajo de 2°, y el mes si el extremo está a menos de un año.
- Validación: JPL Horizons, desde la Tierra (planetodética pasada a planetocéntrica y con el polo IAU) a ≤ 0,02° en 11 fechas; desde el Sol, máximo de 2032 y cruces de 2025 y 2039 (STATUS, paso 5).
- Alternativas: `ring_tilt` tal cual (descartada: signo opuesto al de Horizons y la IAU); la latitud de Horizons sin convertir (descartada: planetodética, 1,7° de más hoy); tendencia mes a mes (descartada: «se van cerrando» en octubre de 2026 confundiría, a cinco años de su máximo); fechas exactas del extremo (descartada: el año basta para algo que tarda siete).

## 041 — Dibujos de las fichas: calculados en Core, como se ven en el cielo, con color y volumen
- Contexto: cada ficha lleva un dibujo de su objeto (Fase 5, ampliada el 7 oct). El usuario eligió dibujos calculados en el momento frente a fotos, tras ver un boceto: la Luna y los planetas con su fase, Júpiter con sus lunas y Saturno con sus anillos.
- Decisión:
  - **Ejes del HUD**: arriba es el cénit y la derecha, hacia donde crece el acimut, como se ve con el móvil derecho (los de la decisión 039). Sin invertir como en un telescopio. El dibujo no gira con el móvil.
  - **En Core** (`BodyDisc`, `PhaseShape`, `SaturnShape`), con tests: la fracción iluminada (la de la decisión 036), la dirección del borde iluminado (el vector cuerpo→Sol proyectado en el plano del cielo) y la del polo norte de la IAU (`RotationAxis`); el contorno de la parte iluminada (medio limbo y media elipse de semieje |1 − 2f|, con área f·π); el globo de Saturno achatado según la inclinación y los anillos de su radio interior (B, 91 975 km) al exterior (A, 136 780 km), con la mitad de delante del lado contrario a la cara que vemos.
  - **En la app** (`CardDrawing`), solo el dibujo. Luna, Mercurio, Venus y Marte, en un disco de 60 dp (no a escala entre sí). Saturno con sus anillos a escala de su globo. Júpiter **como con prismáticos**: un campo oscuro y alargado, Júpiter a escala con sus lunas, la escala ajustada a la luna visible más lejana (al menos 8 radios), las lunas como puntos de luz con su nombre, y las ocultas, eclipsadas o en tránsito sin dibujar.
  - **Color y volumen**: cada objeto con el color con que se ve (Marte anaranjado, Venus crema…), sombreado de esfera con el brillo hacia el Sol, un halo suave y el lado oscuro apenas marcado con su contorno. En modo noche, todo en el rojo de la paleta. Sin relieve, nubes ni casquetes.
  - **Simplificaciones**: Júpiter y Saturno como discos llenos (su fase nunca baja del 99 %); sin sombra del globo sobre los anillos, división de Cassini ni anillo C; sin refracción.
  - **Ficha con pie fijo**: CERRAR siempre a la vista y VER MÁS / VER MENOS para agrandarla hasta el alto del HUD cuando el contenido no cabe. Con el dibujo, la ficha de la Luna ya no cabía, y nadie se daba cuenta de que se podía desplazar.
- Validación: JPL Horizons (ángulos de posición 27 y 17, pasados a los ejes del HUD con el ángulo paraláctico) en 11 casos, a ≤ 0,17° el borde iluminado y ≤ 0,01° el polo; Stellarium Web y el móvil a la misma hora (STATUS, paso 6).
- Alternativas: fotos (descartada por el usuario: no son del momento); ángulos desde el norte celeste (descartada: no es lo que se ve, y la Luna «gira» a lo largo de la noche); `Illumination` geocéntrica (descartada en la decisión 036); escala fija de Júpiter para la máxima elongación de Calisto (descartada en la prueba: casi siempre se veía diminuto); iniciales de las lunas (descartadas en la prueba: esquemático); todo en el color del texto (descartado en la prueba: demasiado esquemático); campo circular de prismáticos (descartado: tan alto como ancho, ocuparía media ficha); desplazar la ficha sin pie (descartada en la prueba: no se descubre).

## 042 — Noticias astronómicas: descartadas; efemérides: fase futura
- Contexto: el usuario propuso dos ideas el 7 oct: un listado de noticias astronómicas sin sensacionalismo y efemérides («tal día como hoy…»), las dos para el público general.
- Decisión:
  - **Noticias, descartadas.** Necesitan red y un criterio editorial diario («nada sensacionalista») que ningún canal garantiza y que la app no puede aplicar sin IA ni servidor (decisión 020). No se revisan ni se contrastan como los textos de las fichas (decisión 037). Y una lista de noticias invita a entrar a mirar qué hay, cuando la app solo pide atención si hay algo que ver (VISION). Para eso ya hay buenas fuentes divulgativas.
  - **Efemérides, como Fase 6** (PLAN): escritas y revisadas como las fichas, con fuente y sin red; solo las ligadas a lo que la app enseña; pocas y buenas; en la ficha del objeto, no en EVENTOS ni como aviso.
- Alternativas: un canal de noticias de una fuente fiable (NASA, ESA, el IAC) leído en la app (descartada: red, sin revisar, y el sensacionalismo lo decide la fuente); efemérides en EVENTOS (descartada por el usuario: EVENTOS es lo que se va a poder ver); una efeméride cada día del año (descartada: 366 textos que revisar, muchos de relleno).

## 043 — Ficha de la ISS: altura sobre el elipsoide, velocidad inercial y por qué se ve o no
- Contexto: la ficha de la ISS solo tenía su texto. Faltaban los datos del momento (Fase 5, paso 7), sin dibujo (decisión 041).
- Decisión:
  - **Altura sobre el elipsoide** (la conversión geodésica de SGP.NET), no sobre una Tierra esférica: la diferencia llega a ~21 km, más de lo que varía su órbita. Al kilómetro.
  - **Velocidad inercial**, alrededor del centro de la Tierra (la de SGP4): es la cifra que da la NASA. La relativa al suelo es hasta 0,4 km/s menor. En km/h, a la centena, y en km por segundo, a la décima.
  - **Distancia a ti**, la misma del HUD, a la decena de km.
  - **Por qué se ve o no**, con las reglas de un paso visible (decisión 011): bajo el horizonte (la ficha se recalcula cada 10 s y puede ponerse con ella abierta) gana a todo; en la sombra de la Tierra no se ve haga el cielo que haga; iluminada, el Sol debe estar por debajo de −6°.
  - Sin el periodo («una vuelta cada 93 minutos»): el texto revisado ya dice «cada hora y media», y apenas cambia.
  - Las frases en Core (`FactsText`), con tests; consola `--card iss`.
- Validación: JPL Horizons, vectores de la ISS (−125544) en ITRF93 para la altura y en ICRF para la velocidad, en 4 instantes con el TLE fijo de los tests: altura ≤ 0,06 km, velocidad ≤ 0,1 m/s (STATUS, paso 7).
- Alternativas: altura sobre una Tierra esférica (descartada: hasta 21 km de error); velocidad respecto al suelo (descartada: no es la que se cita en ninguna parte); «ahora pasa sobre…» un país o ciudad (descartada: necesita un mapa de nombres); solo «iluminada» o «en sombra» (descartada: de día está iluminada y no se ve).

## 044 — Estrellas: paralaje de Hipparcos, años luz según su incertidumbre, color por el tipo espectral
- Contexto: la ficha de las estrellas dice cuánto tarda su luz en llegar (Fase 5). El PLAN preveía la paralaje de SIMBAD, la misma fuente que las coordenadas. Al consultarla, SIMBAD da para 54 de las 155 estrellas la de Gaia, que se satura con estrellas tan brillantes, y no tiene ninguna para β Sco ni γ¹ Leo.
- Decisión:
  - **Paralaje de Hipparcos para las 155** (van Leeuwen 2007, VizieR I/311), con su número HIP y su error, cruzada por posición a < 20″. Coordenadas y magnitudes siguen siendo de SIMBAD. Donde SIMBAD da Gaia, 8 de 54 discrepan de Hipparcos en más de 3σ (Tarazed: 584 frente a 395 años luz); si los dos catálogos fueran fiables, no saldría ni una.
  - **Años luz** con 1 pc = 3,261 563 777 años luz, en la frase de los planetas («Esta luz salió de Sirio hace 8,6 años»), dos cifras significativas y la forma según el error relativo de la paralaje: hasta 5 %, la cifra; hasta 20 %, «unos»; hasta 50 %, «entre» (una desviación a cada lado); más, o si la paralaje no supera su error, «más de», redondeando hacia abajo para que siga siendo cierto. Hoy: 117, 34, 3 (Alnilam, Aludra, ο² CMa) y 1 (Almaaz, 84 %).
  - **Color por la letra del tipo espectral** de SIMBAD, como se enseña en divulgación (O y B azulada, A blanca, F blanco amarillento, G amarillenta, K anaranjada, M rojiza). No por el índice B−V: sus límites habituales están hechos para enanas y en estas gigantes cambian de color a 42 de 155 (Aldebarán «rojiza», Vega «azulada» por una milésima).
  - **Magnitud explicada**: «Brillo: magnitud 0,4 (cuanto menor, más brilla; desde ciudad se ven hasta la 3)». El número solo engaña, porque va al revés.
  - En la ficha, **«DATOS»** en vez de «AHORA» (no cambian con el momento) y un **punto de luz de su color** como dibujo, en rojo de noche. Solo en «¿qué es?», con VER FICHA: las estrellas no son objetivos de la guía.
  - **Límite conocido**: el catálogo solo tiene estrellas con declinación mayor que −50° (decisión 015). Desde el hemisferio sur faltarían Canopus, Alfa Centauri o la Cruz del Sur.
- Validación: Hipparcos en VizieR (los valores de los tests); las 99 en que SIMBAD ya usaba Hipparcos, idénticas; distancias de la NASA para Sirio (8,6) y Vega (25). Betelgeuse: «unos 500» (440-570) frente a 548, 650 o 700 según la página de la NASA; su distancia se discute y lo dirá su texto revisado.
- Alternativas: la paralaje que elige SIMBAD (descartada: Gaia saturada); Gaia DR3 donde discrepe menos (descartada: elegir a mano estrella a estrella); el puesto en brillo «de las que se ven desde España» (descartado por el usuario: la app se puede usar en cualquier país); el puesto en todo el cielo (pospuesto: discutible en dobles y variables; mejor en el texto revisado cuando sea notable); temperatura desde B−V (descartada: errores del 15-20 % en las calientes); «da tanta luz como N soles» (pospuesto a los textos: hereda la incertidumbre de la distancia y es solo luz visible); el tipo espectral tal cual (jerga).

## 045 — La ficha en la guía: VER FICHA siempre visible, sin abrirse sola (corrige 038)
- Contexto: al usar la app, la ficha que se abría sola tras 1,5 s en AQUÍ (decisión 038) estorbaba: tapaba el final de la guía cuando aún se estaba afinando (AQUÍ salta en cuanto la retícula roza el objetivo), obligaba a cerrarla y, una vez cerrada, no había forma de volver a abrirla.
- Decisión (propuesta por el usuario):
  - En modo guía, **VER FICHA siempre visible** para el objetivo elegido, también antes de encontrarlo o con él bajo el horizonte: en la guía ya se sabe qué objeto es. Se abre y se cierra cuantas veces se quiera.
  - Al llegar a **AQUÍ** el botón se **rellena** (como un chip seleccionado): la pista de «ya lo tienes, puedes leer», sin tapar nada.
  - En «¿qué es?», sin cambios: el botón sale con lo reconocido y se mantiene 2 s (`RecentMatch`).
  - Se oculta con el panel de brillo del modo noche, que vive en el mismo sitio. Se quita `CardAutoOpen` (y sus 7 tests).
- Alternativas: el botón solo en AQUÍ (descartada: no se podría consultar la ficha mientras se busca, y en AQUÍ la retícula entra y sale); mantener la apertura sola y añadir el botón (descartada: el modal seguiría saliendo en mitad de la guía); un toque largo sobre el chip del objetivo (descartada: escondido).

## 046 — Textos de las estrellas: qué cuentan, de dónde sale cada dato y por lotes
- Contexto: faltan los textos de las 155 estrellas (Fase 5, paso 9), con el formato de la decisión 037 y el mismo control que los 7 objetivos: borradores con ayuda de IA fuera del repo, revisados uno a uno. Son muchos y la mayoría no tiene la fama de un planeta: hay que fijar qué se cuenta y de dónde sale.
- Decisión:
  - **Qué cuentan**, en dos o tres frases para el público general: lo notable de la estrella (doble, variable, supergigante, gira muy deprisa…), una comparación fácil («en el lugar del Sol, llegaría más allá de la órbita de Júpiter») y el origen del nombre o cómo encontrarla en su figura. Las estrellas con poca historia llevan textos más cortos, no relleno.
  - **Nada de lo que ya calcula la ficha** (decisión 044): ni la distancia, ni el color de su luz, ni la magnitud. Un test impide «años luz» y «magnitud» en los textos de estrellas.
  - **El brillo, solo con el número** (corrige 044, a petición del usuario al ver las fichas con texto): «Brillo: magnitud 2,0», sin «(cuanto menor, más brilla; desde ciudad se ven hasta la 3)». La coletilla ocupaba una segunda línea en cada ficha de estrella.
  - **En la ficha de una estrella, DATOS va antes del texto** (elegido por el usuario): con texto, la ficha plegada solo dejaba ver dos de sus tres datos. Son tres líneas cortas que no cambian, y así se ven siempre sin VER MÁS. La Luna, los planetas y la ISS siguen con el texto antes de AHORA: sus datos son más líneas y, con el dibujo, empujarían el texto fuera de la ficha plegada. El tipo («supergigante roja») sí se puede decir: es lo más notable de Betelgeuse o Antares. El puesto en brillo, cuando es notable («la quinta más brillante del cielo»), sí.
  - **Distancias inciertas**: si las fuentes no coinciden, el texto lo dice sin dar cifra. Betelgeuse: Hipparcos, unos 500 años luz (440-570); Kaler, 495 o 640 según el método; la NASA, 548, 650, 700 o 725 según la página.
  - **Nada que dependa del hemisferio** («estrella de invierno», «desde España»), salvo diciéndolo («desde el hemisferio norte sale poco antes que Sirio»): la app se usa en cualquier país.
  - **Nada que dependa de la hora** (añadido en el lote 2): ni «arriba» ni «abajo» dentro de una figura, porque las figuras giran a lo largo de la noche. Las posiciones, por su parte de la figura («la tercera desde la parte delantera de la caja del Carro», «en la pata central de la Osa Mayor»).
  - **Fuentes**, con cita textual de cada dato: las páginas *Stars* de Jim Kaler (Universidad de Illinois), que cubren casi todo el catálogo; NASA o ESA donde Kaler se ha quedado antiguo (Fomalhaut b, que anunció como planeta, resultó en 2020 una nube de polvo; el oscurecimiento de Betelgeuse de 2019-2020); la NASA Planetary Fact Sheet para las comparaciones con órbitas; la RAE para palabras españolas, comprobada por el usuario. Wikipedia no se cita. Si la fuente dice menos de lo que querría el texto, manda la fuente («de las primeras dobles vistas con telescopio», no «la primera»).
  - **Nombres como en la app**: el título es el nombre que muestra el HUD (Sirio, Estrella Polar) y las constelaciones se nombran como en `SpanishNames` (Escorpio, Tauro, el Can Mayor).
  - **Por lotes de unos 20**, cada uno en su PR con una casilla por texto: primero las 20 más brillantes o conocidas, para fijar el tono; después, por constelación o zona del cielo, para escribir juntas las estrellas de una misma figura sin repetir frases.
- Alternativas: lotes por magnitud (descartada: mezcla figuras y repite la misma idea en lotes distintos); Wikipedia como fuente (descartada: cambia y no es la fuente original); repetir la distancia en el texto para comentarla (descartada: puede contradecir al cálculo); dar la luminosidad como «N soles» (solo redondeada y con su fuente, porque arrastra la incertidumbre de la distancia).

## 047 — Ficha de las constelaciones: tamaño, visibilidad desde tu latitud y sus estrellas, calculados desde los límites de la IAU
- Contexto: las 88 constelaciones tendrán ficha (Fase 5, paso 10). Hay que decidir qué datos lleva, todos calculables o contrastables, sin repetir lo que dirá su texto. Plan acordado con el usuario el 2026-10-08: datos, ficha en la app, dibujo y textos en 5 lotes.
- Decisión:
  - **Tres líneas en DATOS**, como en las estrellas: «Ocupa el 1,4 % del cielo: la 26.ª de 88 por tamaño» («la más grande», la Hidra; «la más pequeña», la Cruz del Sur); «Desde aquí se puede ver entera» / «solo se ve una parte: el resto no llega a salir» / «no sale nunca» / «no se pone nunca»; y «Sus estrellas más brillantes: Rigel, Betelgeuse y Bellatrix» (hasta tres) o «Ninguna de sus estrellas llega a la magnitud 3: desde ciudad cuesta verla».
  - **Área y declinaciones extremas generadas** (`ConstellationExtents.Data.cs`) desde los límites oficiales que ya usa la app (Delporte 1930, digitalizados por Roman 1987, en `Astronomy.Constellation`), con una malla de 0,02° en J2000: el área suma las celdas (exacta, Δα·Δsen δ) y el rango es el de sus bordes. Un script del scratchpad, ~1 min. Las 88 suman todo el cielo (41 252,96 grados cuadrados). El puesto por tamaño y el porcentaje se calculan con esa tabla.
  - **Visibilidad por geometría**, con el criterio de la tabla de Ridpath («rises fully above the horizon at some time»): cada punto de declinación δ sale en algún momento si culmina sobre el horizonte (90° − |φ − δ| > 0), y no se pone si también lo hace su culminación inferior. Sin refracción ni relieve; Escorpio «se puede ver entera» desde Madrid aunque la punta de su cola no pase de 4°. Depende de la latitud del móvil, no del momento.
  - **Estrellas**: las del catálogo dentro de los límites, ordenadas por brillo. Solo si la constelación queda entera al norte de −50°, donde acaba el catálogo (decisión 015): 60 de 88. En las otras 28 la línea no sale: Centauro tiene cinco estrellas en el catálogo, pero no Alfa Centauri, y Erídano no tiene Achernar. Los nombres los pone la app (`SpanishNames`).
  - Las frases, en Core (`FactsText`), con tests. De paso, «e» ante palabras que empiezan por el sonido /i/ en las listas («Júpiter e Ío», «Arturo e Izar»).
  - Sin la línea «N° de punta a punta: X puños con el brazo estirado»: queda para más adelante (decidido por el usuario; necesitaría una fuente para la equivalencia del puño).
- Validación:
  - Áreas frente a las de Levin (1935) que publica Ian Ridpath en su tabla de las constelaciones: las 88 a ≤ 0,15 grados cuadrados, y el mismo puesto en las 88 (también en los pares más apretados: el Tucán y el Indio, 294,6 y 294,0; el Dorado y la Corona Boreal; el Triángulo y el Camaleón).
  - Declinaciones extremas frente a los límites en J2000 del CDS (catálogo VI/49, Davenhall y Leggett 1989): las 88 a ≤ 0,012° (las que rodean un polo, con el polo dentro).
  - Latitudes desde las que cada una sale entera, frente a la tabla de Ridpath: todas a menos de 1°, salvo Leo (83,3° N calculado, 82° N en la tabla). El CDS da para Leo −6,69°, que son 83,3° N: el que no cuadra es la tabla.
  - Las 155 estrellas del catálogo caen dentro de la constelación de su designación de Bayer (SIMBAD), que es otra fuente.
- Alternativas: copiar las áreas de la tabla de Ridpath (descartada: el cálculo propio sale de los mismos límites que usa la app para decir dónde miras, y la tabla sirve para contrastarlo); áreas exactas desde la tabla de límites interna de Astronomy Engine (descartada: es privada; con 0,02° la diferencia con Levin ya es menor que su redondeo); declinaciones de la época actual (descartada: la precesión las mueve ~0,15° desde 2000, nada en una frase que no da grados); las estrellas más brillantes en las 88 aunque el catálogo no llegue (descartada: diría que Menkent es la más brillante de Centauro); un catálogo de estrellas de todo el cielo (fuera del paso: cambiaría el HUD y «¿qué es?»); «grados cuadrados» en la frase (descartada: jerga; el porcentaje del cielo se entiende).

## 048 — En «¿qué es?», el nombre abre la ficha (con una ⓘ), sin botones
- Contexto: la ficha de una constelación se abre desde «¿qué es?» (Fase 5, paso 10.2). La primera versión puso un botón con su nombre junto a VER FICHA. Al probarla en el móvil: VER FICHA iba relleno y el otro solo con borde, y parecía que uno se podía pulsar y el otro no; y al aparecer o desaparecer uno, el otro cambiaba de sitio bajo el dedo (un toque en PERSEO abrió la ficha de Hassaleh).
- Decisión (propuesta por el usuario, revisada en la conversación):
  - En «¿qué es?», **el nombre que muestra el HUD es lo que se toca**: el grande abre la ficha de lo reconocido, y el subtítulo («estrella · altura 12° · en Perseo») la de su constelación. Sin nada reconocido, «Hacia Erídano» abre la de Erídano; la línea de debajo («cerca: Bellatrix…») no abre nada, porque nombra otra cosa.
  - **Una ⓘ detrás de cada nombre que abre algo**, dibujada (círculo, punto y trazo) en el color del texto, también en rojo de noche. No el carácter: la fuente del HUD puede no tenerlo, y el del sistema puede salir como un emoji azul. La ⓘ dice «más información», que es lo que abre; «›» se leía como «siguiente».
  - **Franjas de toque** del panel de texto de lado a lado, de 54 y 58 dp, aunque el texto sea más corto. El texto se encoge hasta caber con su ⓘ («Hacia la Cabellera de Berenice»).
  - **Lo que se nombra no cambia bajo el dedo**: el objeto reconocido sigue 2 s después de perderlo (`RecentMatch`, como hacía VER FICHA) y la constelación se estabiliza en sus límites (`StickyMatch`: sigue la mostrada mientras se haya visto en el último segundo). `RecentMatch` no sirve para ella: siempre hay una constelación bajo la retícula, y con 3-5° de ruido de brújula saltaría de una a otra. La figura y su rótulo usan la misma constelación que el texto.
  - **En la guía, sin cambios**: VER FICHA centrado y relleno al llegar a AQUÍ (decisión 045); ahí el nombre del objetivo va en una línea pequeña y el botón lleva la señal de AQUÍ. Quedan dos formas de abrir la ficha según el modo. El usuario no descarta cambiar también la guía.
- Alternativas: los dos botones con el mismo estilo y en sitios fijos (descartada: funcionaba, pero añade una fila de botones a algo que ya está escrito en pantalla); «›» como pista (descartada: «siguiente», no «información»); el carácter ⓘ de la fuente (descartada: emoji o fuente distinta); solo el icono tocable (descartada: objetivo pequeño con el brazo levantado); la línea «cerca: …» abriendo la constelación (descartada al revisarla: quien la toca espera la estrella que nombra).

## 049 — Dibujo de las constelaciones: estereográfica centrada en la figura, sus estrellas y las de los vértices, tres rótulos
- Contexto: la ficha de una constelación lleva un dibujo como los demás (decisión 041): la figura como se ve en ese momento, con los ejes del HUD, sin línea del horizonte y en rojo de noche (Fase 5, paso 10.3). Las figuras van de 2° a 95° de punta a punta (la Hidra), y el catálogo solo tiene estrellas hasta magnitud 3.
- Decisión:
  - **En Core** (`ConstellationShape`), con tests; en la app, solo el dibujo. Grados de cielo con los ejes de la cámara del HUD (arriba, el cénit; derecha, hacia donde crece el acimut), compartidos con `HudProjection` para que ficha y HUD no puedan discrepar. Se recalcula con la ficha (cada 10 s): la figura gira a lo largo de la noche.
  - **Proyección estereográfica**, no la gnomónica del HUD (decisión 016): conserva la forma y, a 47,7° del centro, agranda ×1,20 por igual en todas direcciones; la gnomónica estiraría los extremos de la Hidra ×2,2 en la dirección radial. En una figura como Orión la diferencia es del 2 %.
  - **Centro**: el del círculo más pequeño del cielo que contiene la figura y sus estrellas (Bădoiu–Clarkson, en J2000, una vez por constelación). Con la media de los vértices, la punta de la Hidra quedaba a 67,6° del centro; así, a 47,7°.
  - **Estrellas**: las del catálogo dentro de los límites (`ConstellationStars.In`), también las que no están en la figura, y las de una vecina en un vértice de la figura (a < 0,05°): sin Alpheratz, al cuadrado de Pegaso le faltaría una esquina. Con su color (`StarColors`) y su tamaño según la magnitud, como en el HUD.
  - **Rótulos**: las tres más brillantes de las suyas, las mismas que nombra DATOS; nunca una vecina (Alpheratz no se rotula en Pegaso). Debajo del punto, encima si choca, y si no cabe, sin rótulo.
  - **Vértices sin estrella del catálogo**, un punto tenue: 42 de 88 constelaciones no tienen ninguna estrella en el catálogo, y Cáncer o Piscis serían rayas que acaban en nada.
  - **Horizonte**: sin línea. Los trazos se cortan donde lo cruzan y lo que no ha salido se pinta apagado, como las referencias bajo el horizonte en el HUD.
  - **Tamaño**: el ancho de la ficha y el alto según la forma (110-170 dp), sin escala común: la Flecha sale tan grande como la Hidra, y el tamaño real lo dice DATOS.
- Validación: Stellarium Web en montura acimutal (Orión, la Osa Mayor, la Hidra y la Serpiente, con la dirección entre dos estrellas, comprobado por el usuario), y en el móvil, la misma figura que el HUD a la misma hora (STATUS, paso 10.3).
- Alternativas: la gnomónica del HUD (descartada: deforma las grandes); centrar en la media de los vértices (descartada: descentra las alargadas); solo `ConstellationStars.In` (descartada: figuras con esquinas vacías); rotular con la regla del HUD, magnitud < 1,7 (descartada: solo 14 de 88 tendrían algún nombre; ninguno la Osa Mayor ni Casiopea); pintarlo todo igual sobre y bajo el horizonte (descartada: diría que se ve lo que no se ve) u ocultar lo de debajo (descartada: rompe la figura); escala común entre constelaciones (descartada: las pequeñas serían un punto); marco de prismáticos como en Júpiter (descartada: las constelaciones se ven a simple vista).
