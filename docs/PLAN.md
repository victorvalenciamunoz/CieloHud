# Plan

**Fase actual: 3**

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

## Fase 4 — Avisos

- Notificaciones locales antes de pasos visibles de la ISS y eventos destacados.

## Fase 5 — IA como guía

- Explicación breve y a nivel no-experto del objeto encontrado, con datos del momento (distancia, fase, lunas visibles…).
