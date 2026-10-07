# Plan

**Fase actual: 5**

---

## Fase 1 — Consola: ¿dónde está cada cosa ahora?

Objetivo: dada una ubicación y un instante, obtener acimut y altura de la Luna, los planetas y la ISS,
y comprobar que coinciden con Stellarium / Heavens-Above.

### Estructura

```
CieloHud.slnx
src/
  CieloHud.Core/
  CieloHud.Console/
tests/
  CieloHud.Core.Tests/
docs/
```

### Core

- `Observer` (latitud, longitud, altitud en metros).
- `HorizontalPosition` (acimut, altura) y quizá distancia.
- `CelestialBody` (Moon, Mercury, Venus, Mars, Jupiter, Saturn) — enum o similar.
- Un servicio para cuerpos del sistema solar: `(body, observer, instant) -> HorizontalPosition`.
  - Candidata: Astronomy Engine (Don Cross), que tiene versión C#. Verificar paquete en NuGet antes de usarlo.
- Un servicio para satélites: `(tle, observer, instant) -> HorizontalPosition`, usando SGP4.
  - Valorar librerías SGP4 para .NET existentes (comparar mantenimiento y licencia). Documentar la elección en DECISIONS.md.
- Un proveedor de TLE:
  - Fuente: CelesTrak (ISS = NORAD 25544). Verificar la URL actual de su API GP.
  - Cache en fichero local; refrescar si tiene más de 24 h.
  - Debe poder usarse un TLE fijo para tests.

### Consola

```
dotnet run --project src/CieloHud.Console -- [--lat 40.4168] [--lon -3.7038] [--time 2026-10-02T21:00:00Z]
```

- Por defecto: Madrid y la hora actual.
- Salida: tabla con objeto, acimut (con punto cardinal: N, NE, E…), altura y "visible/bajo el horizonte".

### Tests

- Con instante y observador fijos, posiciones de la Luna y Júpiter dentro de tolerancia frente a valores de referencia.
- ISS con un TLE fijo guardado en el repo de tests.
- Conversión acimut → punto cardinal.

### Validación (el juez)

- Comparar la salida con Stellarium (o su versión web) para el mismo lugar y hora.
- Tolerancias objetivo: planetas y Luna < 0,5°; ISS < 1° con TLE reciente.
- Apuntar en STATUS.md los resultados de la comparación.

### Hecho cuando

- `dotnet test` en verde.
- La tabla de la consola coincide con Stellarium dentro de tolerancia en al menos 3 instantes distintos.

---

## Fase 2 — Próximos pasos visibles de la ISS

- Calcular pasos en los próximos N días: inicio, punto más alto, fin (hora, acimut, altura).
- "Visible" = ISS iluminada por el Sol Y observador en oscuridad (Sol por debajo de ~-6°) Y altura máxima > 10°.
- Validar contra Heavens-Above.

## Fase 3 — HUD en MAUI (Android)

- Orientación del dispositivo -> hacia qué acimut/altura apunta el móvil.
- Flecha/retícula que guía al objeto elegido. Probar primero con la Luna.
- Tolerar el ruido del magnetómetro: guiar a una zona, no a un píxel.
- Futuro: opción para elegir entre referencias dibujadas (actual) o la cámara de fondo (CommunityToolkit.Maui.Camera, permiso de cámara).
- Referencias en el HUD para orientarse al bajar el móvil: las 15-20 estrellas más brillantes (catálogo fijo pequeño en el repo),
  los demás cuerpos de `CelestialBody` y marcas de horizonte con puntos cardinales. Pocas cosas, todas visibles desde ciudad.
  Nada de líneas de constelaciones ni catálogos grandes. Las estrellas se calculan con la misma conversión ecuatorial → horizontal
  que los planetas (AstronomyEngine `Horizon`).
- Modo "¿qué es eso?": apuntas y la app dice qué objeto del catálogo (Luna, planetas, ISS, estrellas brillantes) está más cerca
  de donde apuntas, si está a menos de ~5°. Es la misma geometría de guiado al revés; no necesita cámara. Hacerlo después de
  validar la precisión en el cielo y junto con el catálogo de estrellas. Es la antesala de la Fase 5.

## Fase 4 — Avisos

- Notificaciones locales antes de pasos visibles de la ISS y eventos destacados.

## Fase 5 — Fichas del objeto

- Al llegar a AQUÍ, o al reconocer algo en "¿qué es?", una ficha breve: qué es, contado para no expertos, y datos del momento.
- Datos del momento calculados en local y contrastables con Horizons o Stellarium:
  - Luna: fase y fracción iluminada (Astronomy Engine MoonPhase, Illumination).
  - Júpiter: lunas galileanas a cada lado (JupiterMoons).
  - Saturno: inclinación de los anillos (Illumination, ring_tilt).
  - Planetas: distancia y tiempo que tarda su luz en llegar.
  - Estrellas: años que tarda su luz en llegar, desde la paralaje de Hipparcos (no la de SIMBAD, que para muchas de estas es Gaia, saturada;
    decisión 044), su color y su brillo explicado.
  - ISS: altura y velocidad.
- Un dibujo de cada objetivo calculado en el momento, no una foto (añadido el 2026-10-07 a petición del usuario): la Luna y los planetas
  con su fase, Júpiter con sus lunas en su sitio y Saturno con la inclinación de sus anillos de ese día. Contrastable con Stellarium y en rojo de noche.
- Textos: uno por objeto (7 objetivos, 155 estrellas, 88 constelaciones), en el repo como datos. Se pueden redactar con ayuda
  de IA durante el desarrollo, pero se revisan uno a uno antes de subirlos. La app no usa IA ni red para esto. Decisión 020.

## Fase 6 — Efemérides (idea apuntada el 2026-10-07, sin planificar)

- «Tal día como hoy…»: un descubrimiento, una misión o una persona, contado para el público general. Decisión 042.
- Solo las ligadas a lo que la app enseña (la Luna, los planetas, la ISS, las estrellas y las constelaciones del catálogo), para que lleven al cielo.
  Una persona entra si descubrió o estudió algo que se puede ver, no por ser relevante en general.
- Pocas y buenas, no una por día: el día que no hay, no sale nada.
- Escritas y revisadas como los textos de las fichas: un Markdown por efeméride, con su fuente, revisado uno a uno. Sin red y sin IA en la app.
- Dónde: en la ficha del objeto (preferencia del usuario). No en EVENTOS, que es lo que se va a poder ver, ni como aviso.
- Por decidir: la ventana (el día exacto, la semana o el mes). Con el día exacto y en la ficha de su objeto, casi nunca coincidirían.

## Idea: guiar también a las estrellas (apuntada el 2026-10-07, sin planificar ni fase asignada)

- Propuesta del usuario al probar las fichas de estrellas: que la guía lleve también a una estrella, como a un planeta. Caso claro: la Estrella Polar
  para encontrar el norte, o «¿dónde está Vega?».
- Cambia lo acordado: VISION dice «pocos objetos que buscar, pero más cosas que se puedan reconocer», y la decisión 044, que las estrellas no son
  objetivos de la guía. Si se hace, va con una decisión nueva que lo corrija.
- Lo técnico ya está: las 155 son `SkyTarget` (`StarTarget`), con su posición calculada; la flecha y AQUÍ sirven tal cual.
- Lo que hay que diseñar es cómo se elige. Idea: un chip ESTRELLAS que abre una caja de texto con autocompletado sobre una lista.
  - Con la caja vacía, las que están sobre el horizonte, ordenadas por brillo: se elige sin escribir y sin saber nombres.
  - Al escribir, filtra las 155 por su nombre en español y el IAU («Sirio», «Sirius»), sin tildes ni mayúsculas, y por designación («gam Cas»).
  - Las que están bajo el horizonte salen marcadas; elegidas, la guía hace lo de siempre con un objetivo bajo el horizonte (no guía).
  - El filtrado y el orden, en Core con sus tests; la pantalla, con `Entry` y `CollectionView` (MAUI no trae autocompletado; sin paquetes).
  - El teclado tapa medio HUD y deslumbra de noche: la lista tiene que bastar sin escribir, y en modo noche, todo en rojo.
- Podría ir junto a las constelaciones (paso 10 de la Fase 5), que también podrían ser destino de la guía.
