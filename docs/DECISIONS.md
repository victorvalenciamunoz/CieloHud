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
