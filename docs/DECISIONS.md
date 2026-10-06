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
